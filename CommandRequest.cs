using System.ComponentModel;

namespace XboxDebugConsole
{
    public enum Command
    {
        [Description("unknown")]
        Unknown,
        [Description("scan")]
        Scan,
        [Description("connect")]
        Connect,
        [Description("disconnect")]
        Disconnect,
        [Description("mute")]
        Mute,
        [Description("unmute")]
        Unmute,
        [Description("loadsymbols")]
        LoadSymbols,
        [Description("setbreak")]
        SetBreakpoint,
        [Description("deletebreak")]
        DeleteBreakpoint,
        [Description("pause")]
        Pause,
        [Description("continue")]
        Continue,
        [Description("read")]
        ReadMemory,
        [Description("dump")]
        DumpMemory,
        [Description("write")]
        WriteMemory,
        [Description("registers")]
        Registers,
        [Description("modules")]
        Modules,
        [Description("threads")]
        Threads,
        [Description("regions")]
        Regions,
        [Description("upload")]
        Upload,
        [Description("launch")]
        Launch,
        [Description("launchdash")]
        LaunchDash,
        [Description("reboot")]
        Reboot,
        [Description("quit")]
        Quit,
        [Description("exit")]
        Exit,
        [Description("help")]
        Help,
        [Description("?")]
        Question,
        [Description("nooutput")]
        NoOutput
    }

    internal sealed record CommandRequest(Command Command, object? Payload)
    {
        public static CommandRequest From<T>(Command command, T payload) => new(command, payload);
    }

    internal sealed record ScanArgs(int TimeoutMs = 5000);

    internal sealed record ConnectArgs(string? Ip = null, string? Name = null, int TimeoutMs = 5000);

    internal sealed record LoadSymbolsArgs(string PdbPath, uint? ImageBase = null);

    internal sealed record AddressArgs(string Address);

    internal sealed record MemoryReadArgs(string Address, int Length);

    internal sealed record MemoryDumpArgs(string Address, int Length, string LocalPath);

    internal sealed record MemoryWriteArgs(string Address, string Data);

    internal sealed record ThreadArgs(int ThreadId = 0);

    internal sealed record UploadArgs(string LocalPath, string RemotePath);

    internal sealed record LaunchArgs(string RemotePath);

    internal sealed record RebootArgs(bool AutoReconnect = true, int TimeoutMs = 10000);

    internal sealed record BreakpointSpec(string? Address = null, string? File = null, int? Line = null);

    internal sealed record BreakpointArgs(IReadOnlyList<BreakpointSpec> Breakpoints);
}
