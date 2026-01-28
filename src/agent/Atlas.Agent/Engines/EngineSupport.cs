using System.Runtime.InteropServices;

namespace Atlas.Agent.Engines;

public static class EngineSupport
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    // Canonical â€œnot supportedâ€ code for a missing platform capability.
    // 126 is the common â€œinvoked command cannot executeâ€ pattern on Unix-like shells.
    public const int NotSupportedExitCode = 126;

    public const string NotSupportedMessage = "not_supported";
}