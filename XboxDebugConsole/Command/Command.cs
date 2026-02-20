using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XboxDebugConsole.Command
{
    public enum Type
    {
        [Description("unknown")]
        Unknown,
        [Description("scan")]
        Scan,
        [Description("connect")]
        Connect,
        [Description("disconnect")]
        Disconnect,
        [Description("reboot")]
        Reboot,
        [Description("mute")]
        Mute,
        [Description("unmute")]
        Unmute,
        [Description("loadsymbols")]
        LoadSymbols,
        [Description("functions")]
        Functions,
        [Description("locals")]
        Locals,
        [Description("setbreak")]
        SetBreakpoint,
        [Description("deletebreak")]
        DeleteBreakpoint,
        [Description("pause")]
        Pause,
        [Description("resume")]
        Resume,
        [Description("read")]
        ReadMemory,
        [Description("dump")]
        DumpMemory,
        [Description("write")]
        WriteMemory,
        [Description("threads")]
        Threads,
        [Description("registers")]
        Registers,
        [Description("modules")]
        Modules,
        [Description("regions")]
        Regions,
        [Description("upload")]
        Upload,
        [Description("launch")]
        Launch,
        //[Description("launchdash")]
        //LaunchDash,

        [Description("quit")]
        Quit,
        [Description("exit")]
        Exit,
        [Description("help")]
        Help,
        [Description("?")]
        Question,
        Count
    }
    
    internal sealed record Request(Type Type, object? Payload)
    {
        public static Request From<T>(Type type, T payload) => new(type, payload);
    }
    
    internal sealed record ScanArgs(int TimeoutMs = 5000);
    
    internal sealed record ConnectArgs(string? Ip = null, string? Name = null, int TimeoutMs = 5000);
    
    internal sealed record RebootArgs(bool AutoReconnect = false, int TimeoutMs = 10000);
    
    internal sealed record LoadSymbolsArgs(string PdbPath, uint? ImageBase = null);
    
    internal sealed record AddressArgs(uint Address);
    
    internal sealed record MemoryReadArgs(uint Address, int Length);
    
    internal sealed record MemoryDumpArgs(uint Address, int Length, string LocalPath);
    
    internal sealed record MemoryWriteArgs(uint Address, byte[] Data);
    
    internal sealed record UploadArgs(string LocalPath, string RemotePath);
    
    internal sealed record LaunchArgs(string RemotePath);
    
    internal sealed record ThreadArgs(int? ThreadId);
    
    internal sealed record BreakpointSpec(uint? Address = null, string File = "", int Line = -1);
    
    internal sealed record BreakpointArgs(IReadOnlyList<BreakpointSpec> Breakpoints);
    
    internal sealed record FunctionArgs(string? file = null);
    
    internal sealed record LocalArgs(int? ThreadId);
    
    internal sealed record Response(Type Type, bool Result, string? Message = null, object? Payload = null)
    {
        public static Response From<T>(Type type, bool result, string? message, T payload) => new(type, result, message, payload);
    }
}
