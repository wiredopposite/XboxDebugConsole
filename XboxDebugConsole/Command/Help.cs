using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxDebugConsole.Command
{
    internal class Help
    {
        internal class Argument
        {
            public string Arg { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
        }

        internal class Description
        {
            public string Type { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public IReadOnlyList<Argument>? Args { get; set; } = null;
        }

        public static readonly Description[] Descriptions = new Description[(int)Type.Count];

        static Help()
        {
            Descriptions[(int)Type.Scan] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Scan),
                Desc = "Scans the network for available Xbox consoles.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "timeoutMs",
                        Desc = "Optional. Time in milliseconds to wait for responses. Default is 5000."
                    }
                }
            };

            Descriptions[(int)Type.Connect] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Connect),
                Desc = "Connects to an Xbox console by IP address, name, or first available.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "ip",
                        Desc = "Optional. IP address of the console to connect to."
                    },
                    new Argument
                    {
                        Arg = "name",
                        Desc = "Optional. Name of the console to connect to."
                    },
                    new Argument
                    {
                        Arg = "timeoutMs",
                        Desc = "Optional. Time in milliseconds to wait for connection. Default is 5000."
                    }
                }
            };

            Descriptions[(int)Type.Disconnect] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Disconnect),
                Desc = "Disconnects from the currently connected Xbox console.",
            };

            Descriptions[(int)Type.Mute] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Mute),
                Desc = "Mutes the notifications from the Xbox.",
            };

            Descriptions[(int)Type.Unmute] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Unmute),
                Desc = "Unmutes the notifications from the Xbox.",
            };

            Descriptions[(int)Type.LoadSymbols] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.LoadSymbols),
                Desc = "Loads symbols from a PDB file for better debugging.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "pdbPath",
                        Desc = "Required. Local path to the PDB file."
                    },
                    new Argument
                    {
                        Arg = "imageBase",
                        Desc = "Optional. Image base address to load symbols at."
                    }
                }
            };

            Descriptions[(int)Type.SetBreakpoint] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.SetBreakpoint),
                Desc = "Sets breakpoints at specified addresses or source lines.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "address",
                        Desc = "Required if no symbols loaded. Address to set a breakpoint at."
                    },
                    new Argument
                    {
                        Arg = "file",
                        Desc = "Required if no address provided. Source file for setting a breakpoint."
                    },
                    new Argument
                    {
                        Arg = "line",
                        Desc = "Required if no address provided. Line number in the source file for the breakpoint."
                    }
                }
            };

            Descriptions[(int)Type.DeleteBreakpoint] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.DeleteBreakpoint),
                Desc = "Deletes breakpoints at specified addresses or source lines.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "address",
                        Desc = "Required if no symbols loaded. Address to set a breakpoint at."
                    },
                    new Argument
                    {
                        Arg = "file",
                        Desc = "Required if no address provided. Source file for setting a breakpoint."
                    },
                    new Argument
                    {
                        Arg = "line",
                        Desc = "Required if no address provided. Line number in the source file for the breakpoint."
                    }
                }
            };

            Descriptions[(int)Type.Upload] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Upload),
                Desc = "Uploads a local file to the Xbox.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "localPath",
                        Desc = "Required. Local path of the file to upload."
                    },
                    new Argument
                    {
                        Arg = "remotePath",
                        Desc = "Required. Remote path on the Xbox to upload the file to."
                    }
                }
            };

            Descriptions[(int)Type.Launch] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Launch),
                Desc = "Launches an application on the Xbox.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "remotePath",
                        Desc = "Required. Remote path of the application on the Xbox to launch."
                    }
                }
            };

            Descriptions[(int)Type.Reboot] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Reboot),
                Desc = "Reboots the Xbox console.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "autoReconnect",
                        Desc = "Optional. Whether to automatically reconnect after reboot. Default is false."
                    },
                    new Argument
                    {
                        Arg = "timeoutMs",
                        Desc = "Optional. Time in milliseconds to wait for the console to come back online. Default is 10000."
                    }
                }
            };

            Descriptions[(int)Type.Threads] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Threads),
                Desc = "Lists all active threads.",
            };

            Descriptions[(int)Type.Registers] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Registers),
                Desc = "Retrieves the register values for a specific thread.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "threadId",
                        Desc = "Optional. ID of the thread to get registers for. Default is first available."
                    }
                }
            };

            Descriptions[(int)Type.Modules] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Modules),
                Desc = "Lists all loaded modules.",
            };

            Descriptions[(int)Type.Regions] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Regions),
                Desc = "Lists all loaded memory regions.",
            };

            Descriptions[(int)Type.Pause] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Pause),
                Desc = "Pauses the execution of the program.",
            };

            Descriptions[(int)Type.Resume] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Resume),
                Desc = "Resumes the execution of the program.",
            };

            Descriptions[(int)Type.ReadMemory] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.ReadMemory),
                Desc = "Reads memory from the Xbox at a specified address.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "address",
                        Desc = "Required. Address to read memory from."
                    },
                    new Argument
                    {
                        Arg = "length",
                        Desc = "Required. Number of bytes to read."
                    }
                }
            };

            Descriptions[(int)Type.WriteMemory] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.WriteMemory),
                Desc = "Writes memory to the Xbox at a specified address.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "address",
                        Desc = "Required. Address to write memory to."
                    },
                    new Argument
                    {
                        Arg = "data",
                        Desc = "Required. Hex string of bytes to write."
                    }
                }
            };

            Descriptions[(int)Type.DumpMemory] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.DumpMemory),
                Desc = "Dumps memory from the Xbox at a specified address to a local file.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "address",
                        Desc = "Required. Address to dump memory from."
                    },
                    new Argument
                    {
                        Arg = "length",
                        Desc = "Required. Number of bytes to dump."
                    },
                    new Argument
                    {
                        Arg = "localPath",
                        Desc = "Required. Local path to save the dumped memory to."
                    }
                }
            };

            Descriptions[(int)Type.Quit] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Quit) + " or " + EnumExtensions.GetDescription(Type.Exit),
                Desc = "Quits the application.",
            };

            Descriptions[(int)Type.Help] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Help) + " or " + EnumExtensions.GetDescription(Type.Question),
                Desc = "Displays this help message.",
            };

            Descriptions[(int)Type.Functions] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Functions),
                Desc = "Lists all functions with optional source file filtering.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "file",
                        Desc = "Optional. Source file to filter functions by."
                    }
                }
            };

            Descriptions[(int)Type.Locals] = new Description
            {
                Type = EnumExtensions.GetDescription(Type.Locals),
                Desc = "Lists all local variables for a specific thread.",
                Args = new[]
                {
                    new Argument
                    {
                        Arg = "threadId",
                        Desc = "Optional. ID of the thread to get local variables for. Default is first available."
                    }
                }
            };
        }
    }
}
