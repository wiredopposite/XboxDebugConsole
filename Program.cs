using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
//using System.CommandLine;
//using System.Threading.Tasks;
//using System;
using System.Reflection;

using ViridiX.Linguist;
using ViridiX.Linguist.Network;
using ViridiX.Linguist.Process;
using ViridiX.Mason.Logging;

namespace XboxDebugConsole
{
    internal class Application
    {
        private static Xbox? _Xbox = null;
        private static ILogger? _Logger = null;
        private static Dictionary<uint, byte[]> _Breakpoints = new Dictionary<uint, byte[]>();
        private static bool _IsRunning = true;
        private static bool _JsonMode = false;
        private const string _DashPath = @"\Device\Harddisk0\Partition1\xboxdash.xbe";
        private static bool _ProcessingCommand = false;
        private static bool _NotificationsMuted = false;
        private static SymbolManager _SymbolManager = new SymbolManager();

        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    _JsonMode = true;
                }
                if (args[i].Equals("mute", StringComparison.OrdinalIgnoreCase))
                {
                    _NotificationsMuted = true;
                }
            }

            if (_JsonMode)
            {
                string? line;
                bool err = false;

                while (_IsRunning && !err)
                {
                    while ((line = Console.ReadLine()) != null)
                    {
                        try
                        {
                            _ProcessingCommand = true;
                            ProcessInputJson(line);
                            Console.Out.Flush();
                            _ProcessingCommand = false;
                        }
                        catch (Exception ex)
                        {
                            WriteError(Command.Unknown, ex.Message);
                            Console.Out.Flush();
                            err = true;
                            break;
                        }
                    }
                }

            }
            else
            {
                _Logger = new SeriLogger(LogLevel.Info);

                Console.Title = "Xbox Debug Console";
                Console.WriteLine("==== Xbox Debug Console ====");
                Console.WriteLine("Type 'help' or '?' for a list of commands.");

                while (_IsRunning)
                {
                    try
                    {
                        Console.Write(_Xbox != null ? "Xbox> " : "> ");
                        string? input = Console.ReadLine()?.Trim();

                        if (string.IsNullOrEmpty(input))
                        {
                            continue;
                        }

                        _ProcessingCommand = true;
                        ProcessInput(input);
                        _ProcessingCommand = false;
                    }
                    catch (Exception ex)
                    {
                        WriteError(Command.Unknown, ex.Message);
                    }
                }
            }

            Cleanup();
        }

        private static void ProcessInput(string input)
        {
            if (!CommandParser.TryParse(input, out var request, out var error))
            {
                WriteError(Command.Unknown, error ?? "Invalid command.");
                return;
            }

            ProcessCommandRequest(request);
        }

        private static void ProcessInputJson(string input)
        {
            if (!JsonCommandParser.TryParse(input, out var request, out var error))
            {
                WriteError(Command.Unknown, error ?? "Invalid command payload.");
                return;
            }

            ProcessCommandRequest(request);
        }

        private static void ProcessCommandRequest(CommandRequest request)
        {
            switch (request.Command)
            {
                case Command.Scan:
                    var scanArgs = request.Payload as ScanArgs ?? new ScanArgs();
                    Scan(request.Command, scanArgs.TimeoutMs);
                    break;

                case Command.Connect:
                    var connectArgs = request.Payload as ConnectArgs ?? new ConnectArgs();
                    Connect(request.Command, connectArgs.Ip, connectArgs.Name, connectArgs.TimeoutMs);
                    break;

                case Command.Disconnect:
                    Disconnect(request.Command);
                    break;

                case Command.LoadSymbols:
                    if (request.Payload is not LoadSymbolsArgs loadArgs)
                    {
                        WriteError(request.Command, "pdbPath is required for loadSymbols command.");
                        break;
                    }

                    LoadSymbols(request.Command, loadArgs);
                    break;

                case Command.SetBreakpoint:
                case Command.DeleteBreakpoint:
                    if (request.Payload is not BreakpointArgs breakpointArgs || breakpointArgs.Breakpoints.Count == 0)
                    {
                        WriteError(request.Command, "breakpoints are required for breakpoint commands.");
                        break;
                    }

                    foreach (var spec in breakpointArgs.Breakpoints)
                    {
                        var address = ResolveBreakpointAddress(spec);
                        if (address == null)
                        {
                            WriteError(request.Command, "Unable to resolve breakpoint address.");
                            continue;
                        }

                        if (request.Command == Command.SetBreakpoint)
                        {
                            SetBreakpoint(request.Command, address);
                        }
                        else
                        {
                            DeleteBreakpoint(request.Command, address);
                        }
                    }
                    break;

                case Command.Pause:
                    Pause(request.Command);
                    break;

                case Command.Continue:
                    Continue(request.Command);
                    break;

                case Command.ReadMemory:
                    if (request.Payload is not MemoryReadArgs readArgs)
                    {
                        WriteError(request.Command, "address and length are required for read command.");
                        break;
                    }

                    ReadMemory(request.Command, readArgs.Address, readArgs.Length);
                    break;

                case Command.DumpMemory:
                    if (request.Payload is not MemoryDumpArgs dumpArgs)
                    {
                        WriteError(request.Command, "address, length, and localPath are required for dump command.");
                        break;
                    }

                    DumpMemory(request.Command, dumpArgs.Address, dumpArgs.Length, dumpArgs.LocalPath);
                    break;

                case Command.WriteMemory:
                    if (request.Payload is not MemoryWriteArgs writeArgs)
                    {
                        WriteError(request.Command, "address and data are required for write command.");
                        break;
                    }

                    WriteMemory(request.Command, writeArgs.Address, writeArgs.Data);
                    break;

                case Command.Registers:
                    var threadArgs = request.Payload as ThreadArgs ?? new ThreadArgs();
                    GetRegisters(request.Command, threadArgs.ThreadId);
                    break;

                case Command.Modules:
                    GetModules(request.Command);
                    break;

                case Command.Threads:
                    GetThreads(request.Command);
                    break;

                case Command.Regions:
                    GetRegions(request.Command);
                    break;

                case Command.Upload:
                    if (request.Payload is not UploadArgs uploadArgs)
                    {
                        WriteError(request.Command, "localPath and remotePath are required for upload command.");
                        break;
                    }

                    UploadFile(request.Command, uploadArgs.LocalPath, uploadArgs.RemotePath);
                    break;

                case Command.Launch:
                    if (request.Payload is not LaunchArgs launchArgs)
                    {
                        WriteError(request.Command, "remotePath is required for launch command.");
                        break;
                    }

                    LaunchFile(request.Command, launchArgs.RemotePath);
                    break;

                case Command.LaunchDash:
                    LaunchFile(request.Command, _DashPath);
                    break;

                case Command.Reboot:
                    var rebootArgs = request.Payload as RebootArgs ?? new RebootArgs();
                    Reboot(request.Command, rebootArgs.AutoReconnect, rebootArgs.TimeoutMs);
                    break;

                case Command.Quit:
                case Command.Exit:
                    _IsRunning = false;
                    break;

                case Command.Question:
                case Command.Help:
                    if (!_JsonMode)
                    {
                        PrintHelp();
                    }
                    else
                    {
                        WriteError(request.Command, "Help command is not available in JSON mode.");
                    }
                    break;

                default:
                    WriteError(Command.Unknown, "Unknown command.");
                    break;
            }
        }

        private static string? ResolveBreakpointAddress(BreakpointSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.Address))
                return spec.Address;

            if (!string.IsNullOrWhiteSpace(spec.File) && spec.Line.HasValue)
            {
                var address = _SymbolManager.GetAddressForLine(spec.File, spec.Line.Value);
                if (address.HasValue)
                    return $"0x{address.Value:X8}";
            }

            return null;
        }

        private static void LoadSymbols(Command command, LoadSymbolsArgs args)
        {
            if (!_SymbolManager.LoadPdb(args.PdbPath))
            {
                WriteError(command, "Failed to load symbols.");
                return;
            }

            if (args.ImageBase.HasValue)
                _SymbolManager.SetImageBase(args.ImageBase.Value);

            WriteSuccess(command, "Symbols loaded.");
        }

        static void WriteNotification(string messageStr)
        {
            if (_NotificationsMuted)
            {
                return;
            }

            while (_ProcessingCommand)
            {
                // If we're currently processing a command, wait a bit before writing the notification
                // This helps prevent interleaving notification output with command results
                System.Threading.Thread.Sleep(100);
            }

            var response = new
            {
                type = "notification",
                timestamp = DateTime.UtcNow,
                message = messageStr
            };

            if (_JsonMode)
            {
                Console.WriteLine(JsonConvert.SerializeObject(response));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n");
                PrintObject(response);
                Console.ResetColor();
                Console.Write("Xbox> ");
            }

            Console.Out.Flush();
        }

        static void Scan(Command command, int timeoutMs = 5000)
        {
            var availableXboxes = Xbox.Discover(timeoutMs, _Logger);
            if (availableXboxes.Count == 0)
            {
                WriteError(command, "No Xbox consoles found on the network.");
                return;
            }

            var xboxList = availableXboxes.Select(x => new
            {
                name = x.Name,
                ip = x.Ip.ToString()
            }).ToArray();

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                count = availableXboxes.Count,
                xboxes = xboxList
            };

            WriteResult(response);
        }

        static void Connect(Command command, string? ipAddress = null, string? xboxName = null, int timeoutMs = 5000)
        {
            if (_Xbox != null)
            {
                WriteError(command, "Already connected to an Xbox. Please disconnect first.");
                return;
            }

            if (ipAddress == null)
            {
                var availableXboxes = Xbox.Discover(timeoutMs, _Logger);

                if (availableXboxes.Count == 0)
                {
                    WriteError(command, "No Xbox consoles found on the network.");
                }

                if (xboxName == null)
                {
                    ipAddress = availableXboxes[0].Ip.ToString();
                }
                else
                {
                    for (int i = 0; i < availableXboxes.Count; i++)
                    {
                        if (availableXboxes[i].Name.Equals(xboxName, StringComparison.OrdinalIgnoreCase))
                        {
                            ipAddress = availableXboxes[i].Ip.ToString();
                            break;
                        }
                    }
                    if (ipAddress == null)
                    {
                        WriteError(command, $"Xbox with name '{xboxName}' not found on the network.");
                        return;
                    }
                }
            }

            try
            {
                _Xbox = new Xbox(_Logger);
                _Xbox.Connect(IPAddress.Parse(ipAddress));

                _Xbox.NotificationReceived += (sender, e) =>
                {
                    WriteNotification(e.Message);

                };
            }
            catch (Exception ex)
            {
                Cleanup();
                WriteError(Command.Connect, "Failed to connect to Xbox: " + ex.Message);
                return;
            }

            WriteSuccess(Command.Connect, $"Successfully connected to Xbox at {ipAddress}.");
        }

        [MemberNotNullWhen(true, nameof(_Xbox))]
        static bool IsConnected(Command command)
        {
            if (_Xbox == null)
            {
                WriteError(command, "Not connected to any Xbox.");
                return false;
            }
            return true;
        }

        static void Disconnect(Command command)
        {
            if (!IsConnected(command))
            {
                return;
            }

            Cleanup();

            WriteSuccess(command, "Disconnected from Xbox.");
        }

        static void Reboot(Command command, bool autoReconnect = true, int timeoutMs = 10000)
        {
            if (!IsConnected(command))
            {
                return;
            }

            var connectedIp = _Xbox.PreviousConnectionAddress;

            try 
            {
                _Xbox.CommandSession.SendCommand("reboot");
            }
            catch
            {

            }

            Cleanup();

            if (!autoReconnect)
            {
                WriteSuccess(command, "Xbox rebooted. Please reconnect when it's back online.");
                return;
            }

            List<XboxConnectionInformation> availableXboxes = new List<XboxConnectionInformation>();

            while (timeoutMs > 0)
            {
                timeoutMs -= 1000;
                availableXboxes = Xbox.Discover(1000, _Logger);

                for (int i = 0; i < availableXboxes.Count; i++)
                {
                    var xbox = availableXboxes[i];
                    if (xbox.Ip.Equals(connectedIp))
                    {
                        Connect(command, xbox.Ip.ToString());
                        return;
                    }
                }
            }

            WriteError(command, "Rebooted Xbox found but failed to reconnect.");
        }

        static void SetBreakpoint(Command command, string addressStr)
        {
            if (!IsConnected(command))
            {
                return;
            }

            uint address = ParseHex(addressStr);
            byte[] ogBytes = _Xbox.Memory.ReadBytes(address, 1);
            var response = _Xbox.DebugMonitor.DmSetBreakpoint(address);

            if (response.Success)
            {
                _Breakpoints[address] = ogBytes;
                WriteSuccess(command, $"Breakpoint set at 0x{address:X8}");
            }
            else
            {
                WriteError(command, $"Failed to set breakpoint at 0x{address:X8}: {response.Message}");
            }
        }

        static void DeleteBreakpoint(Command command, string addressStr)
        {
            if (!IsConnected(command))
            {
                return;
            }

            uint address = ParseHex(addressStr);

            if (_Breakpoints.ContainsKey(address))
            {
                var response = _Xbox.DebugMonitor.DmRemoveBreakpoint(address);

                if (response.Success)
                {
                    _Breakpoints.Remove(address);

                }
                else
                {
                    WriteError(command, $"Failed to remove breakpoint at 0x{address:X8}: {response.Message}");
                    return;
                }
            }

            WriteSuccess(Command.DeleteBreakpoint, $"Breakpoint removed at 0x{address:X8}");
        }

        static void Pause(Command command)
        {
            if (!IsConnected(Command.Pause))
            {
                return;
            }

            var response = _Xbox.DebugMonitor.DmStop();
            if (response.Success)
            {
                WriteSuccess(command, "Execution paused.");
            }
            else
            {
                WriteError(command, "Failed to pause execution: " + response.Message);
            }
        }

        static void Continue(Command command)
        {
            if (!IsConnected(command))
            {
                return;
            }

            var response = _Xbox.DebugMonitor.DmGo();

            if (response.Success)
            {
                WriteSuccess(command, "Execution continued.");
            }
            else
            {
                WriteError(command, "Failed to continue execution: " + response.Message);
            }
        }

        static void ReadMemory(Command command, string addressStr, int readLen)
        {
            if (!IsConnected(command))
            {
                return;
            }

            uint address = ParseHex(addressStr);
            byte[] data = _Xbox.Memory.ReadBytes(address, readLen);
            string hexData = BitConverter.ToString(data).Replace("-", " ");

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                address = $"0x{address:X8}",
                length = readLen,
                data = hexData
            };

            WriteResult(response);
        }

        static void WriteMemory(Command command, string addressStr, string hexStr)
        {
            if (!IsConnected(command))
            {
                return;
            }

            uint address = ParseHex(addressStr);
            string[] hexBytes = hexStr.Split(
                new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] data = hexBytes.Select(
                h => Convert.ToByte(h.Replace("0x", ""), 16)).ToArray();

            _Xbox.Memory.WriteBytes(address, data);

            WriteSuccess(command, $"Wrote {data.Length} bytes to 0x{address:X8}");
        }

        static void DumpMemory(Command command, string addressStr, int length, string filePath)
        {
            if (!IsConnected(command))
            {
                return;
            }

            uint address = ParseHex(addressStr);
            byte[] data = _Xbox.Memory.ReadBytes(address, length);

            File.WriteAllBytes(filePath, data);

            WriteSuccess(command, $"Dumped {length} bytes from 0x{address:X8} to {filePath}");
        }

        static void GetRegisters(Command command, int threadId)
        {
            if (!IsConnected(command))
            {
                return;
            }

            var threads = _Xbox.Process.Threads;
            if (threads.Count == 0)
            {
                WriteError(command, "No threads found in the process.");
                return;
            }

            var thread = (threadId != 0)
                ? threads.FirstOrDefault(t => t.Id == threadId)
                : threads[0];

            if (thread == null)
            {
                WriteError(command, $"Thread not found.");
                return;
            }

            var context = _Xbox.DebugMonitor.DmGetThreadContext(thread.Id, ContextFlags.Full);
            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                threadId = thread.Id,
                registers = new
                {
                    EAX = $"0x{context.Eax:X8}",
                    EBX = $"0x{context.Ebx:X8}",
                    ECX = $"0x{context.Ecx:X8}",
                    EDX = $"0x{context.Edx:X8}",
                    ESI = $"0x{context.Esi:X8}",
                    EDI = $"0x{context.Edi:X8}",
                    EBP = $"0x{context.Ebp:X8}",
                    ESP = $"0x{context.Esp:X8}",
                    EIP = $"0x{context.Eip:X8}",
                    EFlags = $"0x{context.EFlags:X8}",
                    CS = $"0x{context.SegCs:X8}",
                    SS = $"0x{context.SegSs:X8}"
                }
            };

            WriteResult(response);
        }

        static void GetThreads(Command command)
        {
            if (!IsConnected(command))
            {
                return;
            }

            var threads = _Xbox.Process.Threads;

            var threadList = threads.Select(t => new
            {
                id = t.Id,
                suspendCount = t.Suspend,
                priority = t.Priority,
                tlsBase = $"0x{t.TlsBase:X8}",
                start = $"0x{t.Start:X8}",
                stackBase = $"0x{t.Base:X8}",
                stackLimit = $"0x{t.Limit:X8}",
                creationTime = t.CreationTime
            }).ToArray();

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                count = threads.Count,
                threads = threadList
            };

            WriteResult(response);
        }

        static void GetModules(Command command)
        {
            if (!IsConnected(command))
            {
                return;
            }

            var modules = _Xbox.Process.Modules;

            var moduleList = modules.Select(m => new
            {
                name = m.Name,
                baseAddress = $"0x{m.BaseAddress:X8}",
                size = m.Size,
                checksum = $"0x{m.Checksum:X8}",
                timestamp = m.TimeStamp,
                hasTls = m.HasTls,
                isXbe = m.IsXbe,
                sections = m.Sections.Select(s => new
                {
                    name = s.Name,
                    baseAddress = $"0x{s.Base:X8}",
                    size = s.Size,
                    index = s.Index,
                    flags = $"0x{s.Flags:X8}"
                }).ToArray()
            }).ToArray();

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                count = modules.Count,
                modules = moduleList
            };

            WriteResult(response);
        }

        static void GetRegions(Command command)
        {
            if (!IsConnected(command))
            {
                return;
            }

            var regions = _Xbox.Memory.Regions;

            var regionList = regions.Select(r => new
            {
                baseAddress = $"0x{r.Address:X8}",
                size = r.Size,
                //protection  = $"0x{r.Protect:X8}"
                protection = r.Protect.ToString()
            }).ToArray();

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                count = regions.Count,
                regions = regionList
            };

            WriteResult(response);
        }

        static void UploadFile(Command command, string localPath, string remotePath)
        {
            if (!IsConnected(command))
            {
                return;
            }

            if (!File.Exists(localPath))
            {
                WriteError(command, $"Local file not found: {localPath}");
                return;
            }

            byte[] fileBytes = File.ReadAllBytes(localPath);
            _Xbox.FileSystem.WriteFile(remotePath, fileBytes);

            WriteSuccess(command, $"Uploaded file to {remotePath}");
        }

        static void LaunchFile(Command command, string remotePath)
        {
            if (!IsConnected(command))
            {
                return;
            }

            string message;

            try
            {
                var response = _Xbox.CommandSession.SendCommand($"magicboot title=\"{remotePath}\"");

                if (response.Success)
                {
                    WriteSuccess(command, $"Launched {remotePath}");
                    return;
                }
                else 
                {
                    message = response.Message;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            if (message.Contains("timed out"))
            {
                Reboot(Command.NoOutput, autoReconnect: false);
                Disconnect(Command.NoOutput);
                WriteError(command, $"Failed to launch {remotePath}: Console rebooted, please reconnect and try again.");
            }
            else
            {
                WriteError(command, $"Failed to launch {remotePath}: {message}");
            }
        }

        static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("Available commands:\n" +
                "  scan timeoutMs=<MILLISECONDS>\n" +
                "      - Scans the local network for Xbox consoles and displays \n" +
                "        their IP addresses and names. timeoutMs is optional and\n" +
                "        specifies the maximum time in milliseconds to wait for\n" +
                "        responses, default is 5000.\n" +
                "\n" +
                "  connect\n" +
                "      - Connects to the first Xbox console found on the local network.\n" +
                "\n" +
                "  connect name=<CONSOLE_NAME>\n" +
                "      - Connects to the Xbox console with the specified name.\n" +
                "\n" +
                "  connect ip=<IP_ADDRESS>\n" +
                "      - Connects to the Xbox console at the specified IP address.\n" +
                "\n" +
                "  disconnect\n" +
                "      - Disconnects from the currently connected Xbox console.\n" +
                "\n" +
                "  reboot autoReconnect=<BOOL> timeoutMs=<MILLISECONDS>\n" +
                "      - Cold reboot the connected Xbox console. autoReconnect is\n" +
                "        optional, if true, the console will automatically attempt\n" +
                "        to reconnect after  rebooting. timeoutMs is optional and\n" +
                "        specifies the maximum time in milliseconds to wait for \n" +
                "        the console to reconnect, default is 5000.\n" +
                "\n" +
                "  mute\n" +
                "      - Mute console notifications. This will prevent any notifications\n" +
                "        from being displayed until 'unmute' is entered.\n" +
                "\n" +
                "  unmute\n" +
                "      - Unmute console notifications. This will allow notifications\n" +
                "        to be displayed again after being muted.\n" +

                "  upload localPath=<PATH> remotePath=<PATH>\n" +
                "      - Upload a file from the local machine to the Xbox console.\n" +
                "\n" +
                "  launch remotePath=<PATH>\n" +
                "      - Launch an application on the Xbox console from the specified\n" +
                "        remote file path.\n" +
                "\n" +
                "  launchdash\n" +
                "      - Launch the Xbox dashboard.\n" +
                "\n" +
                "  setbreak address=<HEXADDRESS>\n" +
                "      - Set a breakpoint at the specified memory address.\n" +
                "\n" +
                "  deletebreak address=<HEXADDRESS>\n" +
                "      - Clear a breakpoint at the specified memory address.\n" +
                "\n" +
                "  listbreaks\n" +
                "      - List all currently set breakpoints.\n" +
                "\n" +
                "  pause\n" +
                "      - Pause execution of the currently running Xbox application.\n" +
                "\n" +
                "  continue\n" +
                "      - Continue execution of the currently paused Xbox application.\n" +
                "\n" +
                "  read address=<HEXADDRESS> length=<LENGTH>\n" +
                "      - Read a block of memory of the specified length in bytes \n" +
                "        from the specified memory address.\n" +
                "\n" +
                "  dump address=<HEXADDRESS> lenght=<LENGTH> localPath=<PATH>\n" +
                "      - Dump a block of memory of the specified length in bytes \n" +
                "        from the specified memory address to a file.\n" +
                "\n" +
                "  write address=<HEXADDRESS> data=<BYTES>\n" +
                "      - Write a block of data to the specified memory address. \n" +
                "        The data should be provided as a hexadecimal string.\n" +
                "\n" +
                "  modules\n" +
                "      - List all loaded modules on the Xbox console, including\n" +
                "        their base addresses and sizes.\n" +
                "\n" +
                "  threads\n" +
                "      - List all active threads on the Xbox console, including \n" +
                "        their thread IDs and statuses.\n" +
                "\n" +
                "  registers threadId=<THREAD_ID>\n" +
                "      - Display the CPU registers for the specified thread ID. \n" +
                "        If no thread ID is provided, the first found will be used.\n" +
                "\n" +
                "  regions\n" +
                "      - List all memory regions on the Xbox console, including their \n" +
                "        base addresses, sizes, and permissions.\n" +
                "\n" +
                "  exit\n" +
                "  quit\n" +
                "      - Exit the XboxDebugConsole application.\n" +
                "\n" +
                "  ?\n" +
                "  help\n" +
                "      - Display this message.\n" +
                "\n"
            );
            Console.ResetColor();
        }

        static uint ParseHex(string hex)
        {
            hex = hex.Replace("0x", "").Replace("0X", "");
            return Convert.ToUInt32(hex, 16);
        }

        static void Cleanup()
        {
            if (_Xbox != null)
            {
                try
                {
                    _Xbox.Disconnect();
                    _Xbox.Dispose();
                }
                catch
                {
                    // ignore
                }
                _Xbox = null;
            }
            _Breakpoints.Clear();
        }

        static void PrintObject(object obj, int indent = 1)
        {
            if (obj == null)
            {
                Console.WriteLine("null");
                return;
            }

            var type = obj.GetType();
            var indentStr = new string(' ', indent * 2);

            foreach (var prop in type.GetProperties())
            {
                var value = prop.GetValue(obj);
                Console.Write($"{indentStr}{prop.Name}: ");

                if (value == null)
                {
                    Console.WriteLine("null");
                }
                else if (value is string || value.GetType().IsPrimitive || value is DateTime)
                {
                    Console.WriteLine(value);
                }
                else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    Console.WriteLine();
                    int index = 0;
                    foreach (var item in enumerable)
                    {
                        Console.WriteLine($"{indentStr}  [{index}]:");
                        PrintObject(item, indent + 2);
                        index++;
                    }
                }
                else
                {
                    Console.WriteLine();
                    PrintObject(value, indent + 1);
                }
            }
        }

        static void WriteError(Command command, string errorStr)
        {
            if (command == Command.NoOutput)
            {
                return;
            }

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = false,
                message = errorStr
            };

            if (_JsonMode)
            {
                Console.WriteLine(JsonConvert.SerializeObject(response));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                PrintObject(response);
                Console.ResetColor();
            }
        }

        static void WriteSuccess(Command command, string successStr)
        {
            if (command == Command.NoOutput)
            {
                return;
            }

            var response = new
            {
                type = "result",
                command = command.GetDescription(),
                success = true,
                message = successStr
            };

            if (_JsonMode)
            {
                Console.WriteLine(JsonConvert.SerializeObject(response));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                PrintObject(response);
                Console.ResetColor();
            }
        }

        static void WriteResult(object resultObj)
        {
            var commandStr = (resultObj.GetType().GetProperty("command")?.GetValue(resultObj)?.ToString()) ?? "unknown";
            if (commandStr == Command.NoOutput.GetDescription())
            {
                return;
            }

            if (_JsonMode)
            {
                Console.WriteLine(JsonConvert.SerializeObject(resultObj));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                PrintObject(resultObj);
                Console.ResetColor();
            }
        }
    }
}
