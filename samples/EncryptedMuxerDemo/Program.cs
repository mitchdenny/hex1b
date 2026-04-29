using EncryptedMuxerDemo;

var sessionDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".hex1bsamples", "encryptedmuxerdemo");

if (args is ["--server", var socketPath])
{
    await EncryptedMuxerServer.RunAsync(socketPath);
    return;
}

var sessions = new SessionManager(sessionDir);
var app = new SessionManagerApp(sessions);
await app.RunAsync();
