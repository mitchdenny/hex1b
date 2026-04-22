using MuxerDemo;

var sessionDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".hex1bsamples", "muxerdemo");

if (args is ["--server", var socketPath])
{
    await MuxerServer.RunAsync(socketPath);
    return;
}

var sessions = new SessionManager(sessionDir);
var app = new SessionManagerApp(sessions);
await app.RunAsync();
