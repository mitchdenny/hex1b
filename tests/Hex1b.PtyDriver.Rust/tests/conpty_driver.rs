#![cfg(windows)]

use std::collections::HashMap;
use std::time::Duration;

use hex1bpty::ConPtySession;

const DEFAULT_TIMEOUT: Duration = Duration::from_secs(15);
type TestResult<T = ()> =
    std::result::Result<T, Box<dyn std::error::Error + Send + Sync + 'static>>;

#[test]
fn cmd_echo_produces_exact_transcript() -> TestResult {
    let session = ConPtySession::spawn(
        "cmd.exe",
        &["/d", "/q", "/c", "echo __HEX1B_DRIVER_OK__"],
        None,
        &HashMap::new(),
        120,
        40,
    )?;
    let transcript = session.read_transcript_until_exit(DEFAULT_TIMEOUT)?;
    let exit_code = session.wait_for_exit(DEFAULT_TIMEOUT)?;

    assert_eq!(exit_code, 0);
    assert_eq!(
        String::from_utf8(transcript)?,
        format!(
            "{}\u{1b}[H__HEX1B_DRIVER_OK__\r\n{}\u{1b}[?25h",
            vt_setup_prefix(),
            osc_title_sequence(&cmd_title_path())
        )
    );
    Ok(())
}

#[test]
fn powershell_passes_through_exact_ansi_bytes() -> TestResult {
    let session = ConPtySession::spawn(
        "powershell.exe",
        &[
            "-NoLogo",
            "-NoProfile",
            "-Command",
            "$e=[char]27; [Console]::Write($e + '[31mRED' + $e + '[0m')",
        ],
        None,
        &HashMap::new(),
        120,
        40,
    )?;

    let transcript = session.read_transcript_until_exit(DEFAULT_TIMEOUT)?;
    let exit_code = session.wait_for_exit(DEFAULT_TIMEOUT)?;

    assert_eq!(exit_code, 0);
    assert_eq!(
        String::from_utf8(transcript)?,
        format!(
            "{}\u{1b}[31m\u{1b}[HRED{}\u{1b}[?25h",
            vt_setup_prefix(),
            osc_title_sequence(&powershell_title_path())
        )
    );
    Ok(())
}

#[test]
fn powershell_console_api_color_translates_to_expected_vt() -> TestResult {
    let session = ConPtySession::spawn(
        "powershell.exe",
        &[
            "-NoLogo",
            "-NoProfile",
            "-Command",
            "[Console]::ForegroundColor='Green'; [Console]::Write('GREEN'); [Console]::ResetColor()",
        ],
        None,
        &HashMap::new(),
        120,
        40,
    )?;

    let transcript = session.read_transcript_until_exit(DEFAULT_TIMEOUT)?;
    let exit_code = session.wait_for_exit(DEFAULT_TIMEOUT)?;

    assert_eq!(exit_code, 0);
    assert_eq!(
        String::from_utf8(transcript)?,
        format!(
            "{}\u{1b}[38;5;10m\u{1b}[HGREEN{}\u{1b}[?25h",
            vt_setup_prefix(),
            osc_title_sequence(&powershell_title_path())
        )
    );
    Ok(())
}

#[test]
fn interactive_cmd_prompt_accepts_input_and_exits_cleanly() -> TestResult {
    let session = ConPtySession::spawn(
        "cmd.exe",
        &["/q", "/d", "/k", "prompt PTYTEST$G"],
        None,
        &HashMap::new(),
        120,
        40,
    )?;
    let startup = session.read_until_contains(b"\x1b[?25h", DEFAULT_TIMEOUT)?;
    let startup_text = String::from_utf8(startup)?;

    assert_eq!(
        startup_text,
        format!(
            "{}\u{1b}[H{}\u{1b}[?25h",
            vt_setup_prefix(),
            osc_title_sequence(&cmd_prompt_title_path())
        )
    );

    let prompt = session.read_until_contains(b"PTYTEST>", DEFAULT_TIMEOUT)?;
    assert_eq!(
        String::from_utf8(prompt)?,
        format!("\r\nPTYTEST>{}", osc_title_sequence(&cmd_title_path()))
    );

    session.write_input(b"exit\r\n")?;
    let exit_code = session.wait_for_exit(DEFAULT_TIMEOUT)?;
    assert_eq!(exit_code, 0);
    Ok(())
}

fn vt_setup_prefix() -> &'static str {
    "\u{1b}[?9001h\u{1b}[?1004h\u{1b}[?25l\u{1b}[2J\u{1b}[m"
}

fn osc_title_sequence(path: &str) -> String {
    format!("\u{1b}]0;{path}\u{7}")
}

fn cmd_title_path() -> String {
    format!("{}\\SYSTEM32\\cmd.exe", system_root())
}

fn cmd_prompt_title_path() -> String {
    format!("{}\\SYSTEM32\\cmd.exe - prompt  PTYTEST$G", system_root())
}

fn powershell_title_path() -> String {
    format!(
        "{}\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
        system_root()
    )
}

fn system_root() -> String {
    std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\WINDOWS".to_owned())
}
