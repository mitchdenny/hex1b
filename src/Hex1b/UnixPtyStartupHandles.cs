namespace Hex1b;

internal readonly record struct UnixPtyStartupHandles(int MasterFd, int ChildPid, int StartupFd);
