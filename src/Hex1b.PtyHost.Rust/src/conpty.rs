#![cfg(windows)]

use std::collections::HashMap;
use std::ffi::{c_void, OsStr};
use std::fs::File;
use std::io::{self, Read, Write};
use std::mem::{size_of, zeroed};
use std::os::windows::ffi::OsStrExt;
use std::os::windows::io::FromRawHandle;
use std::ptr;
use std::sync::mpsc::{self, Receiver};
use std::sync::Mutex;
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use windows_sys::Win32::Foundation::{
    CloseHandle, GetLastError, SetHandleInformation, HANDLE, HANDLE_FLAG_INHERIT, WAIT_OBJECT_0,
    WAIT_TIMEOUT,
};
use windows_sys::Win32::Security::SECURITY_ATTRIBUTES;
use windows_sys::Win32::System::Console::{
    ClosePseudoConsole, CreatePseudoConsole, ResizePseudoConsole, COORD, HPCON,
};
use windows_sys::Win32::System::Pipes::CreatePipe;
use windows_sys::Win32::System::Threading::{
    CreateProcessW, DeleteProcThreadAttributeList, GetExitCodeProcess,
    InitializeProcThreadAttributeList, TerminateProcess, UpdateProcThreadAttribute,
    WaitForSingleObject, CREATE_UNICODE_ENVIRONMENT, EXTENDED_STARTUPINFO_PRESENT,
    LPPROC_THREAD_ATTRIBUTE_LIST, PROCESS_INFORMATION, STARTUPINFOEXW,
};

const PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE: usize = 0x0002_0016;

pub type BoxError = Box<dyn std::error::Error + Send + Sync + 'static>;
pub type Result<T = ()> = std::result::Result<T, BoxError>;

pub struct ConPtySession {
    pseudo_console: HPCON,
    process_handle: HANDLE,
    thread_handle: HANDLE,
    process_id: u32,
    input_writer: Mutex<File>,
    output_rx: Mutex<Receiver<Vec<u8>>>,
    reader_thread: Mutex<Option<JoinHandle<()>>>,
}

// The Win32 handles carried by ConPtySession are OS-owned resources that may be
// used from multiple threads. Access to the mutable Rust-side state is already
// synchronized with Mutex, so it is sound to mark the session as Send + Sync.
unsafe impl Send for ConPtySession {}
unsafe impl Sync for ConPtySession {}

impl ConPtySession {
    pub fn spawn<S: AsRef<str>>(
        file_name: &str,
        arguments: &[S],
        working_directory: Option<&str>,
        environment: &HashMap<String, String>,
        width: i16,
        height: i16,
    ) -> Result<Self> {
        let mut security_attributes = SECURITY_ATTRIBUTES {
            nLength: size_of::<SECURITY_ATTRIBUTES>() as u32,
            lpSecurityDescriptor: ptr::null_mut(),
            bInheritHandle: 1,
        };

        let (input_read, input_write) = create_pipe_pair(&mut security_attributes)?;
        let (output_read, output_write) = create_pipe_pair(&mut security_attributes)?;

        ensure_non_inheritable(input_write)?;
        ensure_non_inheritable(output_read)?;

        let pseudo_console = create_pseudo_console(input_read, output_write, width, height)?;

        unsafe {
            CloseHandle(input_read);
            CloseHandle(output_write);
        }

        let mut attribute_list_size = 0;
        unsafe {
            InitializeProcThreadAttributeList(ptr::null_mut(), 1, 0, &mut attribute_list_size);
        }

        let mut attribute_list_buffer = vec![0u8; attribute_list_size];
        let attribute_list = attribute_list_buffer.as_mut_ptr() as LPPROC_THREAD_ATTRIBUTE_LIST;

        unsafe {
            if InitializeProcThreadAttributeList(attribute_list, 1, 0, &mut attribute_list_size)
                == 0
            {
                ClosePseudoConsole(pseudo_console);
                return Err(last_error("InitializeProcThreadAttributeList"));
            }

            if UpdateProcThreadAttribute(
                attribute_list,
                0,
                PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                pseudo_console as *mut c_void,
                size_of::<HPCON>(),
                ptr::null_mut(),
                ptr::null_mut(),
            ) == 0
            {
                DeleteProcThreadAttributeList(attribute_list);
                ClosePseudoConsole(pseudo_console);
                return Err(last_error("UpdateProcThreadAttribute"));
            }
        }

        let mut startup_info: STARTUPINFOEXW = unsafe { zeroed() };
        startup_info.StartupInfo.cb = size_of::<STARTUPINFOEXW>() as u32;
        startup_info.lpAttributeList = attribute_list;

        let mut process_information: PROCESS_INFORMATION = unsafe { zeroed() };
        let mut command_line = build_command_line(file_name, arguments);
        let environment_block = build_environment_block(environment);
        let current_directory = working_directory.map(to_wide_string);

        let created = unsafe {
            CreateProcessW(
                ptr::null(),
                command_line.as_mut_ptr(),
                ptr::null(),
                ptr::null(),
                0,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                environment_block
                    .as_ref()
                    .map(|block| block.as_ptr() as *const c_void)
                    .unwrap_or(ptr::null()),
                current_directory
                    .as_ref()
                    .map(|value| value.as_ptr())
                    .unwrap_or(ptr::null()),
                &startup_info.StartupInfo,
                &mut process_information,
            )
        };

        unsafe {
            DeleteProcThreadAttributeList(attribute_list);
        }

        if created == 0 {
            unsafe {
                ClosePseudoConsole(pseudo_console);
                CloseHandle(input_write);
                CloseHandle(output_read);
            }

            return Err(last_error("CreateProcessW"));
        }

        let input_writer = unsafe { File::from_raw_handle(input_write as _) };
        let output_reader = unsafe { File::from_raw_handle(output_read as _) };
        let (output_tx, output_rx) = mpsc::channel();
        let reader_thread = thread::spawn(move || {
            let mut reader = output_reader;
            let mut buffer = [0u8; 4096];

            loop {
                match reader.read(&mut buffer) {
                    Ok(0) => break,
                    Ok(count) => {
                        if output_tx.send(buffer[..count].to_vec()).is_err() {
                            break;
                        }
                    }
                    Err(error) if error.kind() == io::ErrorKind::Interrupted => continue,
                    Err(_) => break,
                }
            }
        });

        Ok(Self {
            pseudo_console,
            process_handle: process_information.hProcess,
            thread_handle: process_information.hThread,
            process_id: process_information.dwProcessId,
            input_writer: Mutex::new(input_writer),
            output_rx: Mutex::new(output_rx),
            reader_thread: Mutex::new(Some(reader_thread)),
        })
    }

    pub fn process_id(&self) -> u32 {
        self.process_id
    }

    pub fn resize(&self, width: i16, height: i16) -> Result {
        let size = COORD {
            X: width.max(1),
            Y: height.max(1),
        };
        let result = unsafe { ResizePseudoConsole(self.pseudo_console, size) };
        if result != 0 {
            return Err(format!("ResizePseudoConsole failed with HRESULT 0x{result:08X}").into());
        }

        Ok(())
    }

    pub fn write_input(&self, data: &[u8]) -> Result {
        let mut writer = self
            .input_writer
            .lock()
            .map_err(|_| io::Error::other("PTY input mutex is poisoned"))?;
        writer.write_all(data)?;
        writer.flush()?;
        Ok(())
    }

    pub fn recv_output_timeout(&self, timeout: Duration) -> Result<Option<Vec<u8>>> {
        let receiver = self
            .output_rx
            .lock()
            .map_err(|_| io::Error::other("PTY output mutex is poisoned"))?;
        match receiver.recv_timeout(timeout) {
            Ok(chunk) => Ok(Some(chunk)),
            Err(mpsc::RecvTimeoutError::Timeout | mpsc::RecvTimeoutError::Disconnected) => Ok(None),
        }
    }

    pub fn read_until_contains(&self, needle: &[u8], timeout: Duration) -> Result<Vec<u8>> {
        let deadline = Instant::now() + timeout;
        let mut transcript = Vec::new();

        while Instant::now() < deadline {
            let remaining = deadline.saturating_duration_since(Instant::now());
            if let Some(chunk) = self.recv_output_timeout(remaining.min(Duration::from_millis(100)))? {
                transcript.extend_from_slice(&chunk);
                if contains_subslice(&transcript, needle) {
                    return Ok(transcript);
                }
            } else if self.has_exited()? && contains_subslice(&transcript, needle) {
                return Ok(transcript);
            }
        }

        Err(format!(
            "timed out waiting for {:?}; transcript was {:?}",
            String::from_utf8_lossy(needle),
            String::from_utf8_lossy(&transcript)
        )
        .into())
    }

    pub fn read_transcript_until_exit(&self, timeout: Duration) -> Result<Vec<u8>> {
        let deadline = Instant::now() + timeout;
        let mut transcript = Vec::new();

        loop {
            while let Some(chunk) = self.recv_output_timeout(Duration::from_millis(1))? {
                transcript.extend_from_slice(&chunk);
            }

            if self.has_exited()? {
                while let Some(chunk) = self.recv_output_timeout(Duration::from_millis(100))? {
                    transcript.extend_from_slice(&chunk);
                }

                break;
            }

            if Instant::now() >= deadline {
                return Err(format!(
                    "timed out waiting for process exit; transcript so far was {:?}",
                    String::from_utf8_lossy(&transcript)
                )
                .into());
            }

            if let Some(chunk) = self.recv_output_timeout(Duration::from_millis(100))? {
                transcript.extend_from_slice(&chunk);
            }
        }

        Ok(transcript)
    }

    pub fn wait_for_exit(&self, timeout: Duration) -> Result<u32> {
        let timeout_ms = timeout.as_millis().min(u32::MAX as u128) as u32;
        let wait_result = unsafe { WaitForSingleObject(self.process_handle, timeout_ms) };

        match wait_result {
            WAIT_OBJECT_0 => current_exit_code(self.process_handle),
            WAIT_TIMEOUT => Err("timed out waiting for child process exit".into()),
            other => Err(format!("WaitForSingleObject returned unexpected result {other}").into()),
        }
    }

    pub fn try_wait_for_exit(&self) -> Result<Option<u32>> {
        let wait_result = unsafe { WaitForSingleObject(self.process_handle, 0) };
        match wait_result {
            WAIT_OBJECT_0 => current_exit_code(self.process_handle).map(Some),
            WAIT_TIMEOUT => Ok(None),
            other => Err(format!("WaitForSingleObject returned unexpected result {other}").into()),
        }
    }

    pub fn has_exited(&self) -> Result<bool> {
        self.try_wait_for_exit().map(|value| value.is_some())
    }

    pub fn kill(&self, exit_code: u32) -> Result {
        unsafe {
            if TerminateProcess(self.process_handle, exit_code) == 0 {
                return Err(last_error("TerminateProcess"));
            }
        }

        Ok(())
    }
}

impl Drop for ConPtySession {
    fn drop(&mut self) {
        unsafe {
            let wait_result = WaitForSingleObject(self.process_handle, 0);
            if wait_result == WAIT_TIMEOUT {
                let _ = TerminateProcess(self.process_handle, 1);
                let _ = WaitForSingleObject(self.process_handle, 1000);
            }

            if !self.process_handle.is_null() {
                CloseHandle(self.process_handle);
            }

            if !self.thread_handle.is_null() {
                CloseHandle(self.thread_handle);
            }

            if self.pseudo_console != 0 {
                ClosePseudoConsole(self.pseudo_console);
            }
        }

        if let Ok(mut reader_thread) = self.reader_thread.lock() {
            if let Some(reader_thread) = reader_thread.take() {
                let _ = reader_thread.join();
            }
        }
    }
}

fn current_exit_code(process_handle: HANDLE) -> Result<u32> {
    let mut exit_code = 0;
    unsafe {
        if GetExitCodeProcess(process_handle, &mut exit_code) == 0 {
            return Err(last_error("GetExitCodeProcess"));
        }
    }

    Ok(exit_code)
}

fn create_pipe_pair(security_attributes: &mut SECURITY_ATTRIBUTES) -> Result<(HANDLE, HANDLE)> {
    let mut read_handle = HANDLE::default();
    let mut write_handle = HANDLE::default();

    unsafe {
        if CreatePipe(&mut read_handle, &mut write_handle, security_attributes, 0) == 0 {
            return Err(last_error("CreatePipe"));
        }
    }

    Ok((read_handle, write_handle))
}

fn ensure_non_inheritable(handle: HANDLE) -> Result {
    unsafe {
        if SetHandleInformation(handle, HANDLE_FLAG_INHERIT, 0) == 0 {
            return Err(last_error("SetHandleInformation"));
        }
    }

    Ok(())
}

fn create_pseudo_console(input_read: HANDLE, output_write: HANDLE, width: i16, height: i16) -> Result<HPCON> {
    let mut pseudo_console = HPCON::default();
    let size = COORD {
        X: width.max(1),
        Y: height.max(1),
    };
    let result =
        unsafe { CreatePseudoConsole(size, input_read, output_write, 0, &mut pseudo_console) };

    if result != 0 {
        return Err(format!("CreatePseudoConsole failed with HRESULT 0x{result:08X}").into());
    }

    Ok(pseudo_console)
}

fn build_command_line<S: AsRef<str>>(file_name: &str, arguments: &[S]) -> Vec<u16> {
    let mut command_line = String::new();
    push_quoted_argument(&mut command_line, file_name);

    for argument in arguments {
        command_line.push(' ');
        push_quoted_argument(&mut command_line, argument.as_ref());
    }

    to_wide_string(&command_line)
}

fn build_environment_block(environment: &HashMap<String, String>) -> Option<Vec<u16>> {
    if environment.is_empty() {
        return None;
    }

    let mut entries = environment.iter().collect::<Vec<_>>();
    entries.sort_unstable_by(|left, right| left.0.cmp(right.0));

    let mut block = String::new();
    for (key, value) in entries {
        block.push_str(key);
        block.push('=');
        block.push_str(value);
        block.push('\0');
    }
    block.push('\0');

    Some(to_wide_string(&block))
}

fn to_wide_string(value: &str) -> Vec<u16> {
    OsStr::new(value)
        .encode_wide()
        .chain(std::iter::once(0))
        .collect()
}

fn push_quoted_argument(command_line: &mut String, argument: &str) {
    if !argument.contains([' ', '\t', '"']) {
        command_line.push_str(argument);
        return;
    }

    command_line.push('"');
    let mut backslash_count = 0;

    for character in argument.chars() {
        match character {
            '\\' => backslash_count += 1,
            '"' => {
                command_line.push_str(&"\\".repeat(backslash_count * 2 + 1));
                command_line.push('"');
                backslash_count = 0;
            }
            _ => {
                if backslash_count > 0 {
                    command_line.push_str(&"\\".repeat(backslash_count));
                    backslash_count = 0;
                }

                command_line.push(character);
            }
        }
    }

    if backslash_count > 0 {
        command_line.push_str(&"\\".repeat(backslash_count * 2));
    }

    command_line.push('"');
}

fn contains_subslice(haystack: &[u8], needle: &[u8]) -> bool {
    haystack
        .windows(needle.len())
        .any(|window| window == needle)
}

fn last_error(operation: &str) -> BoxError {
    let error = unsafe { GetLastError() };
    format!(
        "{operation} failed with Win32 error {error}: {}",
        io::Error::last_os_error()
    )
    .into()
}
