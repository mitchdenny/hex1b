namespace Hex1b;

internal interface IUnixPtyStartupInterop
{
    void ValidateLibrary();
    UnixPtyStartupHandles Begin(string executable, string[] arguments, string workingDirectory,
        string[] environment, int width, int height);
    int Poll(int startupFd, int timeoutMilliseconds, ref UnixPtyStartupState state, out int stage, out int error);
    void CloseStartup(int startupFd);
    void Abort(UnixPtyStartupHandles handles);
}
