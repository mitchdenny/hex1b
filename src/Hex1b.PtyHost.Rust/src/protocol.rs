use std::collections::HashMap;
use std::io::{self, Read, Write};

use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(u8)]
pub enum FrameType {
    LaunchRequest = 1,
    Started = 2,
    Output = 3,
    Input = 4,
    Resize = 5,
    Kill = 6,
    Exit = 7,
    Error = 8,
    Shutdown = 9,
}

impl TryFrom<u8> for FrameType {
    type Error = io::Error;

    fn try_from(value: u8) -> io::Result<Self> {
        match value {
            1 => Ok(Self::LaunchRequest),
            2 => Ok(Self::Started),
            3 => Ok(Self::Output),
            4 => Ok(Self::Input),
            5 => Ok(Self::Resize),
            6 => Ok(Self::Kill),
            7 => Ok(Self::Exit),
            8 => Ok(Self::Error),
            9 => Ok(Self::Shutdown),
            _ => Err(io::Error::new(
                io::ErrorKind::InvalidData,
                format!("unknown PTY shim frame type {value}"),
            )),
        }
    }
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LaunchRequest {
    pub file_name: String,
    pub arguments: Vec<String>,
    pub working_directory: Option<String>,
    #[serde(default)]
    pub environment: HashMap<String, String>,
    pub width: i32,
    pub height: i32,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StartedResponse {
    pub process_id: u32,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ResizeRequest {
    pub width: i32,
    pub height: i32,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExitNotification {
    pub exit_code: u32,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ErrorResponse {
    pub message: String,
}

pub fn write_frame<W: Write>(writer: &mut W, frame_type: FrameType, payload: &[u8]) -> io::Result<()> {
    writer.write_all(&[frame_type as u8])?;
    writer.write_all(&(payload.len() as i32).to_le_bytes())?;
    if !payload.is_empty() {
        writer.write_all(payload)?;
    }

    writer.flush()
}

pub fn write_json<W: Write, T: Serialize>(
    writer: &mut W,
    frame_type: FrameType,
    value: &T,
) -> io::Result<()> {
    let payload = serde_json::to_vec(value).map_err(json_error)?;
    write_frame(writer, frame_type, &payload)
}

pub fn read_frame<R: Read>(reader: &mut R) -> io::Result<Option<(FrameType, Vec<u8>)>> {
    let mut header = [0u8; 5];
    let header_bytes = read_exact_or_eof(reader, &mut header)?;
    if header_bytes == 0 {
        return Ok(None);
    }

    if header_bytes != header.len() {
        return Err(io::Error::new(
            io::ErrorKind::UnexpectedEof,
            "the PTY shim connection closed while reading a frame header",
        ));
    }

    let payload_length = i32::from_le_bytes([header[1], header[2], header[3], header[4]]);
    if payload_length < 0 {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            format!("invalid PTY shim payload length {payload_length}"),
        ));
    }

    let payload_length = payload_length as usize;
    let mut payload = vec![0u8; payload_length];
    if payload_length > 0 {
        let payload_bytes = read_exact_or_eof(reader, &mut payload)?;
        if payload_bytes != payload_length {
            return Err(io::Error::new(
                io::ErrorKind::UnexpectedEof,
                "the PTY shim connection closed while reading a frame payload",
            ));
        }
    }

    Ok(Some((FrameType::try_from(header[0])?, payload)))
}

pub fn read_json<T: DeserializeOwned>(payload: &[u8]) -> io::Result<T> {
    serde_json::from_slice(payload).map_err(json_error)
}

fn read_exact_or_eof<R: Read>(reader: &mut R, buffer: &mut [u8]) -> io::Result<usize> {
    let mut total_read = 0;
    while total_read < buffer.len() {
        match reader.read(&mut buffer[total_read..]) {
            Ok(0) => return Ok(total_read),
            Ok(bytes_read) => total_read += bytes_read,
            Err(error) if error.kind() == io::ErrorKind::Interrupted => continue,
            Err(error)
                if matches!(
                    error.kind(),
                    io::ErrorKind::TimedOut | io::ErrorKind::WouldBlock
                ) && total_read > 0 =>
            {
                continue;
            }
            Err(error) => return Err(error),
        }
    }

    Ok(total_read)
}

fn json_error(error: serde_json::Error) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, error)
}
