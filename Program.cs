using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Reflection;

using ViridiX.Linguist;
using ViridiX.Linguist.Network;
using ViridiX.Linguist.Process;
using ViridiX.Mason.Logging;
using XboxDebugConsole.Command;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace XboxDebugConsole
{
    internal class XboxNotification
    {
        public string Type { get; set; } = "notification";
        public string Timestamp { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    internal class Application
    {
        private static Xbox? _Xbox = null;
        private static ILogger? _Logger = null;
        private static Dictionary<uint, byte[]> _Breakpoints = new Dictionary<uint, byte[]>();
        private static bool _IsRunning = true;
        private static bool _JsonMode = false;
        private const string _DashPath = @"B:\xboxdash.xbe";
        private static SymbolManager _SymbolManager = new SymbolManager();
        private static bool _NotificationsMuted = false;
        private static ConcurrentQueue<XboxNotification> _XboxNotifications = new ConcurrentQueue<XboxNotification>();

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
                AppTaskJson();

            }
            else
            {
                AppTask();
            }

            Cleanup();
        }

        private static void AppTask()
        {
            _Logger = new SeriLogger(LogLevel.Info);

            Console.Title = "Xbox Debug Console";
            Console.WriteLine("==== Xbox Debug Console ====");
            Console.WriteLine("Type 'help' or '?' for a list of commands.");

            while (_IsRunning)
            {
                Console.Write(_Xbox != null ? "Xbox> " : "> ");
                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                ProcessInput(input);
                Console.Out.Flush();
                ProcessNotifications();
                Console.Out.Flush();
            }
        }

        private static void AppTaskJson()
        {
            string? line;
            //bool err = false;

            while (_IsRunning)
            {
                while (_IsRunning && ((line = Console.ReadLine()) != null))
                {
                    ProcessInput(line);
                    Console.Out.Flush();
                    ProcessNotifications();
                    Console.Out.Flush();
                }
            }
        }

        private static void ProcessNotifications()
        {
            while (_XboxNotifications.TryDequeue(out var notification))
            {
                if (_JsonMode)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(notification));
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    PrintObject(notification);
                    Console.ResetColor();
                }
            }
        }

        private static void HandleResponse(Command.Response response)
        {
            if (_JsonMode)
            {
                Console.WriteLine(JsonConvert.SerializeObject(response));
            }
            else
            {
                Console.ForegroundColor = response.Result ? ConsoleColor.Green : ConsoleColor.Red;
                PrintObject(response);
                Console.ResetColor();
            }
        }

        private static void ProcessInput(string input)
        {
            if (_JsonMode)
            {
                if (!Command.ParserJson.TryParse(input, out var request, out var error))
                {
                    HandleResponse(
                        GenericError(
                            new Command.Request(Command.Type.Unknown, null), error ?? "Invalid command payload."));
                    return;
                }

                ProcessRequest(request);
            }
            else
            {
                if (!Command.Parser.TryParse(input, out var request, out var error))
                {
                    HandleResponse(
                        GenericError(
                            new Command.Request(Command.Type.Unknown, null), error ?? "Invalid command."));
                    return;
                }

                ProcessRequest(request);
            }
        }   

        private static void ProcessRequest(Command.Request request)
        {
            Command.Response? response = null;

            switch (request.Type)
            {
                case Command.Type.Scan:
                    response = Scan(request);
                    break;
                case Command.Type.Connect:
                    response = Connect(request);
                    break;
                case Command.Type.Disconnect:
                    response = Disconnect(request);
                    break;
                case Command.Type.LoadSymbols:
                    response = LoadSymbols(request);
                    break;
                case Command.Type.SetBreakpoint:
                    response = SetBreakpoints(request);
                    break;
                case Command.Type.DeleteBreakpoint:
                    response = DeleteBreakpoints(request);
                    break;
                case Command.Type.Pause:
                    response = Pause(request);
                    break;
                case Command.Type.Resume:
                    response = Continue(request);
                    break;
                case Command.Type.ReadMemory:
                    response = ReadMemory(request);
                    break;
                case Command.Type.DumpMemory:
                    response = DumpMemory(request);
                    break;
                case Command.Type.WriteMemory:
                    response = WriteMemory(request);
                    break;
                case Command.Type.Registers:
                    response = GetRegisters(request);
                    break;
                case Command.Type.Modules:
                    response = GetModules(request);
                    break;
                case Command.Type.Threads:
                    response = GetThreads(request);
                    break;
                case Command.Type.Regions:
                    response = GetRegions(request);
                    break;
                case Command.Type.Upload:
                    response = UploadFile(request);
                    break;
                case Command.Type.Launch:
                    response = LaunchFile(request);
                    break;
                //case Command.Type.LaunchDash:
                //    response = LaunchFile(request, _DashPath);
                //    break;
                case Command.Type.Reboot:
                    response = Reboot(request);
                    break;
                case Command.Type.Quit:
                case Command.Type.Exit:
                    _IsRunning = false;
                    return;

                case Command.Type.Question:
                case Command.Type.Help:
                    if (!_JsonMode)
                    {
                        PrintHelp();
                        return;
                    }
                    else
                    {
                        response = GenericError(request, "Help command is not available in JSON mode.");
                    }
                    break;

                default:
                    break;
            }

            if (response == null)
            {
                response = GenericError(request, "Unknown command.");
            }

            HandleResponse(response);
        }

        [MemberNotNullWhen(true, nameof(_Xbox))]
        private static bool XboxConnected()
        {
            return (_Xbox == null) ? false : true;
        }

        private static Command.Response GenericError(Command.Request request, string? message = null)
        {
            return new Command.Response(request.Type, false, message);
        }

        private static Command.Response GenericSuccess(Command.Request request, string? message = null)
        {
            return new Command.Response(request.Type, true, message);
        }

        private static Command.Response NotConnectedError(Command.Request request)
        {
            return GenericError(request, "Not connected to any Xbox console.");
        }

        private static Command.Response LoadSymbols(Command.Request request)
        {
            if (request.Payload is not Command.LoadSymbolsArgs args)
            {
                return GenericError(request, "PdbPath is required for loadsymbols command.");
            }

            if (!_SymbolManager.LoadPdb(args.PdbPath))
            {
                return GenericError(request, "Failed to load symbols.");
            }

            if (args.ImageBase.HasValue)
                _SymbolManager.SetImageBase(args.ImageBase.Value);

            return GenericSuccess(request, "Symbols loaded.");
        }

        static Command.Response Scan(Command.Request request)
        {
            var args = request.Payload as Command.ScanArgs ?? new Command.ScanArgs();

            var availableXboxes = Xbox.Discover(args.TimeoutMs, _Logger);
            if (availableXboxes.Count == 0)
            {
                return GenericError(request, "No Xbox consoles found on the network.");
            }

            var xboxList = availableXboxes.Select(x => new
            {
                name = x.Name,
                ip = x.Ip.ToString()
            }).ToArray();

            return new Command.Response(request.Type, true, null, xboxList);
        }

        private static bool ConnectRaw(string ipAddress)
        {
            try
            {
                _Xbox = new Xbox(_Logger);
                _Xbox.Connect(IPAddress.Parse(ipAddress));

                _Xbox.NotificationReceived += (sender, e) =>
                {
                    HandleNotification(e.Message);
                };
            }
            catch
            {
                Cleanup();
                return false;
            }

            return true;
        }

        private static Command.Response Connect(Command.Request request)
        {
            if (_Xbox != null)
            {
                return GenericError(request, "Already connected to an Xbox. Please disconnect first.");
            }

            var args = request.Payload as Command.ConnectArgs ?? new Command.ConnectArgs();
            string? ipAddress = args.Ip;

            if (ipAddress == null)
            {
                //var timeout = args.TimeoutMs;
                var availableXboxes = Xbox.Discover(args.TimeoutMs, _Logger);

                if (availableXboxes.Count == 0)
                {
                    return GenericError(request, "No Xbox consoles found on the network.");
                }

                if (args.Name == null)
                {
                    ipAddress = availableXboxes[0].Ip.ToString();
                }
                else
                {
                    for (int i = 0; i < availableXboxes.Count; i++)
                    {
                        if (availableXboxes[i].Name.Equals(args.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            ipAddress = availableXboxes[i].Ip.ToString();
                            break;
                        }
                    }
                    if (ipAddress == null)
                    {
                        return GenericError(request, $"Xbox with name '{args.Name}' not found on the network.");
                    }
                }
            }

            if (!ConnectRaw(ipAddress))
            {
                return GenericError(request, $"Failed to connect to Xbox at {ipAddress}.");
            }

            return GenericSuccess(request, $"Successfully connected to Xbox at {ipAddress}.");
        }

        private static Command.Response Disconnect(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            Cleanup();

            return GenericSuccess(request, "Disconnected from Xbox.");
        }

        private static Command.Response Reboot(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            var args = request.Payload as Command.RebootArgs ?? new Command.RebootArgs();
            var connectedIp = _Xbox.PreviousConnectionAddress;

            try 
            {
                _Xbox.CommandSession.SendCommand("reboot");
            }
            catch
            {

            }

            Cleanup();

            if (!args.AutoReconnect)
            {
                return GenericSuccess(request, "Xbox rebooted. Please reconnect when it's back online.");
            }

            List<XboxConnectionInformation> availableXboxes = new List<XboxConnectionInformation>();
            var timeoutMs = args.TimeoutMs;

            while (timeoutMs > 0)
            {
                timeoutMs -= 1000;
                availableXboxes = Xbox.Discover(1000, _Logger);

                for (int i = 0; i < availableXboxes.Count; i++)
                {
                    var xbox = availableXboxes[i];
                    if (xbox.Ip.Equals(connectedIp))
                    {
                        if (ConnectRaw(xbox.Ip.ToString()))
                        {
                            return GenericSuccess(request, "Xbox rebooted and reconnected successfully.");
                        }
                    }
                }
            }

            return GenericError(request, "Rebooted Xbox found but failed to reconnect within the timeout period.");
        }

        private static Command.Response SetBreakpoints(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not Command.BreakpointArgs args)
            {
                return GenericError(request, "address or file/line is required for setbreakpoint command.");
            }

            var results = new List<object>();

            foreach (var bp in args.Breakpoints)
            {
                uint? address = bp.Address;
                string file = bp.File;
                int line = bp.Line;

                if (address == null)
                {
                    if (string.IsNullOrEmpty(file) || (line < 0) ||
                        !_SymbolManager.Initialized() ||
                        ((address = _SymbolManager.GetAddressForLine(file, line)) == null))
                    {
                        results.Add(new 
                        {
                            success = false,
                            address = 0,
                            file = file ?? "",
                            line = (line > 0) ? line : 0,
                            message = "Address is required if symbols are not loaded."
                        });
                        continue;
                    }
                }

                bool result = false;
                string? errMsg = null;

                try
                {
                    byte[] ogBytes = _Xbox.Memory.ReadBytes(address.Value, 1);
                    var response = _Xbox.DebugMonitor.DmSetBreakpoint(address.Value);

                    if (response.Success)
                    {
                        _Breakpoints[address.Value] = ogBytes;
                        result = true;
                    }

                    errMsg = response.Message;
                }
                catch (Exception ex)
                {
                    errMsg = ex.Message;
                }

                results.Add(new
                {
                    success = result,
                    address = address ?? 0,
                    file = file ?? "",
                    line = (line > 0) ? line : 0,
                    message = errMsg
                });
            }

            bool anySuccess = results.Any(r => ((bool)r.GetType().GetProperty("success")?.GetValue(r) == true));

            if (anySuccess)
            {
                return new Command.Response(request.Type, true, null, results);
            }
            else
            {
                return GenericError(request, "No valid breakpoints provided.");
            }
        }

        private static Command.Response DeleteBreakpoints(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not Command.BreakpointArgs args)
            {
                return GenericError(request, "address or file/line is required for deletebreakpoint command.");
            }

            var addressList = new List<uint>();

            foreach (var bp in args.Breakpoints)
            {
                uint? address = bp.Address;
                string file = bp.File;
                int line = bp.Line;

                if (address == null)
                {
                    if (string.IsNullOrEmpty(file) || (line < 0) ||
                        !_SymbolManager.Initialized() ||
                        ((address = _SymbolManager.GetAddressForLine(file, line)) == null))
                    {
                        continue;
                    }
                }

                addressList.Add(address.Value);
            }

            foreach (var address in addressList)
            {
                if (!_Breakpoints.ContainsKey(address))
                {
                    continue;
                }

                try
                {
                    _Xbox.DebugMonitor.DmRemoveBreakpoint(address);
                }
                catch
                {
                }

                _Breakpoints.Remove(address);
            }

            return GenericSuccess(request, "Breakpoints deleted.");
        }

        private static Command.Response Pause(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            try
            {
                var response = _Xbox.DebugMonitor.DmStop();
                if (response.Success)
                {
                    return GenericError(request, response.Message);
                }
                else
                {
                    return GenericSuccess(request, "Execution paused.");
                }
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }
        }

        private static Command.Response Continue(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            try
            {
                var response = _Xbox.DebugMonitor.DmGo();
                if (response.Success)
                {
                    return GenericSuccess(request, "Execution continued.");
                }
                else
                {
                    return GenericError(request, response.Message);
                }
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }
        }

        private static Command.Response ReadMemory(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not MemoryReadArgs args)
            {
                return GenericError(request, "address and length are required for read command.");
            }

            try
            {
                byte[] data = _Xbox.Memory.ReadBytes(args.Address, args.Length);
                var payload = new
                {
                    address = $"0x{args.Address:X8}",
                    length = data.Length,
                    data = BitConverter.ToString(data).Replace("-", " ")
                };
                return new Command.Response(request.Type, true, null, payload);
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }
        }

        private static Command.Response WriteMemory(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not MemoryWriteArgs args)
            {
                return GenericError(request, "address and data are required for write command.");
            }

            try
            {
                _Xbox.Memory.WriteBytes(args.Address, args.Data);
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }

            return GenericSuccess(request, $"Wrote {args.Data.Length} bytes to 0x{args.Address:X8}");
        }

        private static Command.Response DumpMemory(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not MemoryDumpArgs args)
            {
                return GenericError(request, "address, length and localPath are required for dump command.");
            }

            try
            {
                byte[] data = _Xbox.Memory.ReadBytes(args.Address, args.Length);
                File.WriteAllBytes(args.LocalPath, data);
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }

            return GenericSuccess(request, $"Dumped {args.Length} bytes from 0x{args.Address:X8} to {args.LocalPath}");
        }

        private static Command.Response GetRegisters(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            var threads = _Xbox.Process.Threads;
            if (threads.Count == 0)
            {
                return GenericError(request, "No threads found in the process.");
            }

            if (request.Payload is not Command.ThreadArgs args)
            {
                return GenericError(request, "threadId is required for registers command.");
            }

            var thread = (args.ThreadId != 0)
                ? threads.FirstOrDefault(t => t.Id == args.ThreadId)
                : threads[0];

            if (thread == null)
            {
                return GenericError(request, $"Thread with ID {args.ThreadId} not found.");
            }

            try
            {
                var context = _Xbox.DebugMonitor.DmGetThreadContext(thread.Id, ContextFlags.Full);
                var result = new
                {
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
                return new Command.Response(request.Type, true, null, result);
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }
        }

        private static Command.Response GetThreads(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            var threadList = _Xbox.Process.Threads.Select(t => new
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

            if (threadList.Length == 0)
            {
                return GenericError(request, "No threads found in the process.");
            }

            return new Command.Response(request.Type, true, null, threadList);
        }

        private static Command.Response GetModules(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            var moduleList = _Xbox.Process.Modules.Select(m => new
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

            if (moduleList.Length == 0)
            {
                return GenericError(request, "No modules found in the process.");
            }

            return new Command.Response(request.Type, true, null, moduleList);
        }

        private static Command.Response GetRegions(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            var regionList = _Xbox.Memory.Regions.Select(r => new
            {
                baseAddress = $"0x{r.Address:X8}",
                size = r.Size,
                protection = r.Protect.ToString()
            }).ToArray();

            if (regionList.Length == 0)
            {
                return GenericError(request, "No memory regions found.");
            }

            return new Command.Response(request.Type, true, null, regionList);
        }

        private static Command.Response UploadFile(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not Command.UploadArgs args)
            {
                return GenericError(request, "localPath and remotePath are required for upload command.");
            }

            if (!File.Exists(args.LocalPath))
            {
                return GenericError(request, $"Local file not found: {args.LocalPath}");
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(args.LocalPath);
                _Xbox.FileSystem.WriteFile(args.RemotePath, fileBytes);
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }

            return GenericSuccess(request, $"Uploaded {args.LocalPath} to {args.RemotePath}");
        }

        private static Command.Response LaunchFile(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not Command.LaunchArgs args)
            {
                return GenericError(request, "remotePath is required for launch command.");
            }

            string message;

            try
            {
                var response = _Xbox.CommandSession.SendCommand($"magicboot title=\"{args.RemotePath}\"");

                if (response.Success)
                {
                    return GenericSuccess(request, $"Launched {args.RemotePath} successfully."); 
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
                Reboot(new Command.Request(Command.Type.Reboot, null));
                Disconnect(new Command.Request(Command.Type.Disconnect, null));
                return GenericError(request, $"Failed to launch {args.RemotePath}: Console rebooted, please reconnect and try again.");
            }
            else
            {
                return GenericError(request, message);
            }
        }

        private static void WriteWrapped(string text, int indent, int maxWidth)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();

            foreach (var word in words)
            {
                if (indent + line.Length + word.Length + 1 > maxWidth)
                {
                    Console.WriteLine(new string(' ', indent) + line.ToString().TrimEnd());
                    line.Clear();
                }

                line.Append(word).Append(' ');
            }

            if (line.Length > 0)
            {
                Console.WriteLine(new string(' ', indent) + line.ToString().TrimEnd());
            }
        }

        static void PrintHelp()
        {
            ConsoleColor consoleColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("==== Available commands ====");
            var consoleLimit = 64;

            foreach (var desc in Command.HelpCatalog.Descriptions)
            {
                if (desc == null)
                {
                    continue;
                }
                else if (string.IsNullOrEmpty(desc.Type))
                {
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                WriteWrapped(desc.Type, 2, consoleLimit);

                Console.ForegroundColor = ConsoleColor.Gray;
                WriteWrapped(desc.Description, 4, consoleLimit);

                if (desc.Arguments != null)
                {
                    WriteWrapped("Params:", 4, consoleLimit);

                    foreach (var arg in desc.Arguments)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        WriteWrapped(arg.Arg, 6, consoleLimit);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        WriteWrapped(arg.Description, 8, consoleLimit);
                    }
                }
                Console.Write("\n");
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("==== Usage ====");
            Console.ForegroundColor = ConsoleColor.Yellow;
            WriteWrapped("scan timeoutMs=5000", 2, consoleLimit);
            WriteWrapped("connect ip=192.168.0.1", 2, consoleLimit);
            Console.Write("\n");

            Console.ForegroundColor = consoleColor;
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
                else if (value is Command.Type cmdtype)
                {
                    Console.WriteLine(cmdtype.GetDescription());
                }
                else
                {
                    Console.WriteLine();
                    PrintObject(value, indent + 1);
                }
            }
        }

        static void HandleNotification(string messageStr)
        {
            if (_NotificationsMuted)
            {
                return;
            }

            _XboxNotifications.Enqueue(new XboxNotification
            {
                Timestamp = DateTime.UtcNow.ToString(),
                Message = messageStr
            });
        }
    }
}
