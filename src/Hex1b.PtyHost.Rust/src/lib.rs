#![cfg_attr(not(windows), allow(dead_code))]

#[cfg(windows)]
pub mod conpty;
#[cfg(windows)]
pub mod protocol;

#[cfg(windows)]
pub use conpty::ConPtySession;
#[cfg(windows)]
pub use protocol::{
    ErrorResponse, ExitNotification, FrameType, LaunchRequest, ResizeRequest, StartedResponse,
};
