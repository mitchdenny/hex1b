#[cfg(not(windows))]
fn main() {
    eprintln!("hex1bpty only runs on Windows.");
    std::process::exit(1);
}

#[cfg(windows)]
fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

#[cfg(windows)]
fn run() -> Result<(), Box<dyn std::error::Error + Send + Sync + 'static>> {
    use std::env;
    use std::fs::OpenOptions;
    use std::io::Write;
    use std::io;
    use std::path::{Path, PathBuf};
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::sync::Arc;
    use std::thread;
    use std::time::{Duration, Instant};

    use hex1bpty::protocol::{read_frame, read_json, write_frame, write_json};
    use hex1bpty::{
        ConPtySession, ErrorResponse, ExitNotification, FrameType, LaunchRequest, ResizeRequest,
        StartedResponse,
    };
    use socket2::{Domain, SockAddr, Socket, Type};

    let socket_path = parse_socket_path(env::args().skip(1))?;
    let trace_enabled = env::var_os("HEX1B_PTY_SHIM_TRACE").is_some();
    delete_socket_file(&socket_path);

    let listener = Socket::new(Domain::UNIX, Type::STREAM, None)?;
    listener.bind(&SockAddr::unix(&socket_path)?)?;
    listener.listen(1)?;

    let result = (|| -> Result<(), Box<dyn std::error::Error + Send + Sync + 'static>> {
        let (mut stream, _) = listener.accept()?;
        stream.set_read_timeout(Some(Duration::from_millis(50)))?;

        let Some((frame_type, payload)) = read_frame(&mut stream)? else {
            return Err("the PTY shim client disconnected before sending a launch request".into());
        };

        if frame_type != FrameType::LaunchRequest {
            return Err("the first PTY shim frame must be a launch request".into());
        }

        let mut request: LaunchRequest = read_json(&payload)?;
        trace_message(
            trace_enabled,
            format!(
                "hex1bpty launch: file={} args={:?} cwd={:?} size={}x{}",
                request.file_name, request.arguments, request.working_directory, request.width, request.height
            ),
        );
        request
            .environment
            .insert("HEX1B_PTY_SHIM_ACTIVE".to_owned(), "1".to_owned());

        let session = match ConPtySession::spawn(
            &request.file_name,
            &request.arguments,
            request.working_directory.as_deref(),
            &request.environment,
            clamp_dimension(request.width),
            clamp_dimension(request.height),
        ) {
            Ok(session) => Arc::new(session),
            Err(error) => {
                let _ = write_json(
                    &mut stream,
                    FrameType::Error,
                    &ErrorResponse {
                        message: error.to_string(),
                    },
                );
                return Err(error);
            }
        };

        write_json(
            &mut stream,
            FrameType::Started,
            &StartedResponse {
                process_id: session.process_id(),
            },
        )?;

        let stop_output = Arc::new(AtomicBool::new(false));
        let output_stop = Arc::clone(&stop_output);
        let output_session = Arc::clone(&session);
        let mut output_stream = stream.try_clone()?;
        let output_thread = thread::spawn(move || {
            pump_output(&mut output_stream, output_session, output_stop, trace_enabled)
        });

        let exit_code = pump_commands_until_exit(&mut stream, &session, &stop_output, trace_enabled)?;
        stop_output.store(true, Ordering::Release);

        match output_thread.join() {
            Ok(Ok(())) => {}
            Ok(Err(error)) if error.kind() == io::ErrorKind::BrokenPipe => {}
            Ok(Err(error)) => return Err(error.into()),
            Err(_) => return Err("the PTY shim output pump panicked".into()),
        }

        let _ = write_json(
            &mut stream,
            FrameType::Exit,
            &ExitNotification { exit_code },
        );

        Ok(())
    })();

    delete_socket_file(&socket_path);

    fn parse_socket_path<I>(mut args: I) -> Result<PathBuf, Box<dyn std::error::Error + Send + Sync + 'static>>
    where
        I: Iterator<Item = String>,
    {
        while let Some(argument) = args.next() {
            if argument.eq_ignore_ascii_case("--socket") {
                let value = args.next().ok_or("missing value after --socket")?;
                return Ok(PathBuf::from(value));
            }
        }

        Err("Usage: hex1bpty --socket <path>".into())
    }

    fn delete_socket_file(path: &Path) {
        if path.exists() {
            let _ = std::fs::remove_file(path);
        }
    }

    fn clamp_dimension(value: i32) -> i16 {
        value.clamp(1, i16::MAX as i32) as i16
    }

    fn pump_output(
        stream: &mut Socket,
        session: Arc<ConPtySession>,
        stop: Arc<AtomicBool>,
        trace_enabled: bool,
    ) -> io::Result<()> {
        let mut saw_exit = false;

        loop {
            match session.recv_output_timeout(Duration::from_millis(25)) {
                Ok(Some(chunk)) => {
                    saw_exit = false;
                    let mut combined = chunk;
                    let deadline = Instant::now() + Duration::from_millis(10);

                    while Instant::now() < deadline {
                        match session.recv_output_timeout(Duration::from_millis(1)) {
                            Ok(Some(next)) => combined.extend_from_slice(&next),
                            Ok(None) => break,
                            Err(error) => return Err(other_error(error)),
                        }
                    }

                    trace_message(
                        trace_enabled,
                        format!("hex1bpty output: {:?}", String::from_utf8_lossy(&combined)),
                    );
                    write_frame(stream, FrameType::Output, &combined)?;
                }
                Ok(None) => {
                    if stop.load(Ordering::Acquire) {
                        break;
                    }

                    if session.has_exited().map_err(other_error)? {
                        if saw_exit {
                            break;
                        }

                        saw_exit = true;
                    }
                }
                Err(error) => return Err(other_error(error)),
            }
        }

        while let Some(chunk) = session
            .recv_output_timeout(Duration::from_millis(10))
            .map_err(other_error)?
        {
            trace_message(
                trace_enabled,
                format!("hex1bpty output: {:?}", String::from_utf8_lossy(&chunk)),
            );
            write_frame(stream, FrameType::Output, &chunk)?;
        }

        Ok(())
    }

    fn pump_commands_until_exit(
        stream: &mut Socket,
        session: &ConPtySession,
        stop_output: &AtomicBool,
        trace_enabled: bool,
    ) -> Result<u32, Box<dyn std::error::Error + Send + Sync + 'static>> {
        loop {
            if let Some(exit_code) = session.try_wait_for_exit()? {
                trace_message(trace_enabled, format!("hex1bpty exit: {exit_code}"));
                return Ok(exit_code);
            }

            match read_frame(stream) {
                Ok(Some((frame_type, payload))) => match frame_type {
                    FrameType::Input => {
                        trace_message(
                            trace_enabled,
                            format!("hex1bpty input: {:?}", String::from_utf8_lossy(&payload)),
                        );
                        session.write_input(&payload)?
                    }
                    FrameType::Resize => {
                        let resize: ResizeRequest = read_json(&payload)?;
                        trace_message(trace_enabled, format!("hex1bpty resize: {}x{}", resize.width, resize.height));
                        session.resize(clamp_dimension(resize.width), clamp_dimension(resize.height))?;
                    }
                    FrameType::Kill => {
                        trace_message(trace_enabled, "hex1bpty kill requested".to_owned());
                        let _ = session.kill(15);
                    }
                    FrameType::Shutdown => {
                        trace_message(trace_enabled, "hex1bpty shutdown requested".to_owned());
                        stop_output.store(true, Ordering::Release);
                        let _ = session.kill(15);
                        return session.wait_for_exit(Duration::from_secs(5));
                    }
                    _ => {}
                },
                Ok(None) => {
                    trace_message(trace_enabled, "hex1bpty client disconnected".to_owned());
                    stop_output.store(true, Ordering::Release);
                    let _ = session.kill(15);
                    return session.wait_for_exit(Duration::from_secs(5));
                }
                Err(error)
                    if matches!(
                        error.kind(),
                        io::ErrorKind::TimedOut | io::ErrorKind::WouldBlock
                    ) => {}
                Err(error) => return Err(error.into()),
            }
        }
    }

    fn other_error(error: Box<dyn std::error::Error + Send + Sync + 'static>) -> io::Error {
        io::Error::new(io::ErrorKind::Other, error.to_string())
    }

    fn trace_message(trace_enabled: bool, message: String) {
        if trace_enabled {
            eprintln!("{message}");
        }

        if let Some(path) = env::var_os("HEX1B_PTY_SHIM_TRACE_FILE") {
            if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(path) {
                let _ = writeln!(file, "{message}");
            }
        }
    }

    result
}
