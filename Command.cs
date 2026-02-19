using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxDebugConsole
{
    namespace Command
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
            //[Description("nooutput")]
            //NoOutput,
            Count
        }

        internal class HelpArgument
        {
            public string Arg { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        internal class HelpDescription
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public IReadOnlyList<HelpArgument>? Arguments { get; set; } = null;
        }

        internal static class HelpCatalog
        {
            public static readonly HelpDescription[] Descriptions = new HelpDescription[(int)Type.Count];

            static HelpCatalog()
            {
                Descriptions[(int)Type.Scan] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Scan),
                    Description = "Scans the network for available Xbox consoles.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "timeoutMs",
                            Description = "Optional. Time in milliseconds to wait for responses. Default is 5000."
                        }
                    }
                };

                Descriptions[(int)Type.Connect] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Connect),
                    Description = "Connects to an Xbox console by IP address, name, or first available.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "ip",
                            Description = "Optional. IP address of the console to connect to."
                        },
                        new HelpArgument
                        {
                            Arg = "name",
                            Description = "Optional. Name of the console to connect to."
                        },
                        new HelpArgument
                        {
                            Arg = "timeoutMs",
                            Description = "Optional. Time in milliseconds to wait for connection. Default is 5000."
                        }
                    }
                };

                Descriptions[(int)Type.Disconnect] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Disconnect),
                    Description = "Disconnects from the currently connected Xbox console.",
                };

                Descriptions[(int)Type.Mute] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Mute),
                    Description = "Mutes the notifications from the Xbox.",
                };

                Descriptions[(int)Type.Unmute] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Unmute),
                    Description = "Unmutes the notifications from the Xbox.",
                };

                Descriptions[(int)Type.LoadSymbols] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.LoadSymbols),
                    Description = "Loads symbols from a PDB file for better debugging.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "pdbPath",
                            Description = "Required. Local path to the PDB file."
                        },
                        new HelpArgument
                        {
                            Arg = "imageBase",
                            Description = "Optional. Image base address to load symbols at."
                        }
                    }
                };

                Descriptions[(int)Type.SetBreakpoint] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.SetBreakpoint),
                    Description = "Sets breakpoints at specified addresses or source lines.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "address",
                            Description = "Required if no symbols loaded. Address to set a breakpoint at."
                        },
                        new HelpArgument
                        {
                            Arg = "file",
                            Description = "Required if no address provided. Source file for setting a breakpoint."
                        },
                        new HelpArgument
                        {
                            Arg = "line",
                            Description = "Required if no address provided. Line number in the source file for the breakpoint."
                        }
                    }
                };

                Descriptions[(int)Type.DeleteBreakpoint] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.DeleteBreakpoint),
                    Description = "Sets breakpoints at specified addresses or source lines.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "address",
                            Description = "Required if no symbols loaded. Address to set a breakpoint at."
                        },
                        new HelpArgument
                        {
                            Arg = "file",
                            Description = "Required if no address provided. Source file for setting a breakpoint."
                        },
                        new HelpArgument
                        {
                            Arg = "line",
                            Description = "Required if no address provided. Line number in the source file for the breakpoint."
                        }
                    }
                };

                Descriptions[(int)Type.Upload] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Upload),
                    Description = "Uploads a local file to the Xbox.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "localPath",
                            Description = "Required. Local path of the file to upload."
                        },
                        new HelpArgument
                        {
                            Arg = "remotePath",
                            Description = "Required. Remote path on the Xbox to upload the file to."
                        }
                    }
                };

                Descriptions[(int)Type.Launch] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Launch),
                    Description = "Launches an application on the Xbox.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "remotePath",
                            Description = "Required. Remote path of the application on the Xbox to launch."
                        }
                    }
                };

                Descriptions[(int)Type.Reboot] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Reboot),
                    Description = "Reboots the Xbox console.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "autoReconnect",
                            Description = "Optional. Whether to automatically reconnect after reboot. Default is false."
                        },
                        new HelpArgument
                        {
                            Arg = "timeoutMs",
                            Description = "Optional. Time in milliseconds to wait for the console to come back online. Default is 10000."
                        }
                    }
                };

                Descriptions[(int)Type.Threads] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Threads),
                    Description = "Lists all active threads.",
                };

                Descriptions[(int)Type.Registers] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Registers),
                    Description = "Retrieves the register values for a specific thread.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "threadId",
                            Description = "Optional. ID of the thread to get registers for. Default is first available."
                        }
                    }
                };

                Descriptions[(int)Type.Modules] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Modules),
                    Description = "Lists all loaded modules.",
                };

                Descriptions[(int)Type.Regions] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Regions),
                    Description = "Lists all loaded memory regions.",
                };

                Descriptions[(int)Type.Pause] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Pause),
                    Description = "Pauses the execution of the program.",
                };

                Descriptions[(int)Type.Resume] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Resume),
                    Description = "Resumes the execution of the program.",
                };

                Descriptions[(int)Type.ReadMemory] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.ReadMemory),
                    Description = "Reads memory from the Xbox at a specified address.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "address",
                            Description = "Required. Address to read memory from."
                        },
                        new HelpArgument
                        {
                            Arg = "length",
                            Description = "Required. Number of bytes to read."
                        }
                    }
                };

                Descriptions[(int)Type.WriteMemory] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.WriteMemory),
                    Description = "Writes memory to the Xbox at a specified address.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "address",
                            Description = "Required. Address to write memory to."
                        },
                        new HelpArgument
                        {
                            Arg = "data",
                            Description = "Required. Hex string of bytes to write."
                        }
                    }
                };

                Descriptions[(int)Type.DumpMemory] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.DumpMemory),
                    Description = "Dumps memory from the Xbox at a specified address to a local file.",
                    Arguments = new[]
                    {
                        new HelpArgument
                        {
                            Arg = "address",
                            Description = "Required. Address to dump memory from."
                        },
                        new HelpArgument
                        {
                            Arg = "length",
                            Description = "Required. Number of bytes to dump."
                        },
                        new HelpArgument
                        {
                            Arg = "localPath",
                            Description = "Required. Local path to save the dumped memory to."
                        }
                    }
                };

                Descriptions[(int)Type.Quit] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Quit) + " or " + EnumExtensions.GetDescription(Type.Exit),
                    Description = "Quits the application.",
                };

                Descriptions[(int)Type.Help] = new HelpDescription
                {
                    Type = EnumExtensions.GetDescription(Type.Help) + " or " + EnumExtensions.GetDescription(Type.Question),
                    Description = "Displays this help message.",
                };
            }
        }

        internal sealed record Request(Type Type, object? Payload)
        {
            public static Request From<T>(Type type, T payload) => new(type, payload);
        }

        internal sealed record ScanArgs(int TimeoutMs = 5000);

        internal sealed record ConnectArgs(string? Ip = null, string? Name = null, int TimeoutMs = 5000);

        internal sealed record LoadSymbolsArgs(string PdbPath, uint? ImageBase = null);

        internal sealed record AddressArgs(uint Address);

        internal sealed record MemoryReadArgs(uint Address, int Length);

        internal sealed record MemoryDumpArgs(uint Address, int Length, string LocalPath);

        internal sealed record MemoryWriteArgs(uint Address, byte[] Data);

        internal sealed record ThreadArgs(int ThreadId = 0);

        internal sealed record UploadArgs(string LocalPath, string RemotePath);

        internal sealed record LaunchArgs(string RemotePath);

        internal sealed record RebootArgs(bool AutoReconnect = false, int TimeoutMs = 10000);

        internal sealed record BreakpointSpec(uint? Address = null, string File = "", int Line = -1);

        internal sealed record BreakpointArgs(IReadOnlyList<BreakpointSpec> Breakpoints);

        internal sealed record Response(Type Type, bool Result, string? Message = null, object? Payload = null)
        {
            public static Response From<T>(Type type, bool result, string? message, T payload) => new(type, result, message, payload);
        }
    }
}
