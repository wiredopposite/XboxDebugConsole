using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using ViridiX.Linguist;
using ViridiX.Linguist.Network;
using ViridiX.Linguist.Process;
using ViridiX.Mason.Logging;
//using XboxDebugConsole.Command;
//using static System.Runtime.InteropServices.JavaScript.JSType;

namespace XboxDebugConsole
{
    internal class Application
    {
        internal class XboxNotification
        {
            public string Type { get; set; } = "notification";
            public string Timestamp { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

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

                if (!string.IsNullOrEmpty(input))
                {
                    ProcessInput(input);
                    Console.Out.Flush();
                }

                ProcessNotifications();
                Console.Out.Flush();
            }
        }

        private static void AppTaskJson()
        {
            string? line;

            while (_IsRunning)
            {
                while (_IsRunning && ((line = Console.ReadLine()) != null))
                {
                    ProcessInput(line);
                    Console.Out.Flush();
                }

                ProcessNotifications();
                Console.Out.Flush();
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
                            new Command.Request(Command.Type.Unknown, null), 
                            error ?? "Invalid command payload."));
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
                            new Command.Request(Command.Type.Unknown, null), 
                            error ?? "Invalid command."));
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
                case Command.Type.Functions:
                    response = GetFunctions(request);
                    break;
                //case Command.Type.Variables:
                //    response = GetVariables(request);
                //    break;
                case Command.Type.Locals:
                    response = GetLocals(request);
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
                    response = UploadFiles(request);
                    break;
                case Command.Type.Launch:
                    response = Launch(request);
                    break;
                //case Command.Type.LaunchDash:
                //    response = LaunchFile(request, _DashPath);
                //    break;
                case Command.Type.Reboot:
                    response = Reboot(request);
                    break;
                case Command.Type.Mute:
                    _NotificationsMuted = true;
                    return;
                case Command.Type.Unmute:
                    _NotificationsMuted = false;
                    return;
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

        private static Command.Response ArgsError(Command.Request request, string? message = null)
        {
            return GenericError(request, message ?? "Invalid or missing arguments for the command.");
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
            {
                _SymbolManager.SetImageBase(args.ImageBase.Value);
            }

            return GenericSuccess(request, "Symbols loaded.");
        }

        private static Command.Response GetFunctions(Command.Request request)
        {
            if (!_SymbolManager.Initialized())
            {
                return GenericError(request, "Symbols not loaded.");
            }
            
            if (request.Payload is not Command.FunctionArgs args)
            {
                return ArgsError(request);
            }

            var functions = _SymbolManager.GetFunctions(args.File)
                .Select(f => new
                {
                    name = f.Name,
                    rva = $"0x{f.Rva:X8}",
                    address = $"0x{f.Address:X8}",
                    length = f.Length
                })
                .ToArray();

            if (functions.Length == 0)
            {
                return GenericError(request, "No functions found in symbols.");
            }

            return new Command.Response(request.Type, true, null, functions);
        }

        private static Command.Response GetLocals(Command.Request request)
        {
            if (!_SymbolManager.Initialized())
            {
                return GenericError(request, "Symbols not loaded.");
            }
            else if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not Command.ThreadArgs args)
            {
                return ArgsError(request);
            }

            var threads = _Xbox.Process.Threads;
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
                var locals = _SymbolManager.GetLocalsForAddress(context.Eip, context.Ebp)
                    .Select(l => new
                    {
                        name = l.Name,
                        type = l.TypeName,
                        size = l.Size,
                        address = l.Address.HasValue ? $"0x{l.Address:X8}" : null,
                        isParamater = l.IsParameter,
                        value = l.Address.HasValue ?
                            BitConverter.ToString(_Xbox.Memory.ReadBytes(l.Address.Value, (int)l.Size)).Replace("-", " ") :
                            ""
                    })
                    .ToArray();

                return new Command.Response(request.Type, true, null, locals);
            }
            catch (Exception ex)
            {
                return GenericError(request, ex.Message);
            }
        }

        //private static Command.Response GetLocation(Command.Request request)
        //{
        //    if (!_SymbolManager.Initialized())
        //    {
        //        return GenericError(request, "Symbols not loaded.");
        //    }
            
        //    if (request.Payload is not Command.LocationArgs args)
        //    {
        //        return ArgsError(request);
        //    }
        //}

        private static Command.Response Scan(Command.Request request)
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

        private static bool ConnectRaw(string ipAddress, out string? error)
        {
            error = null;

            try
            {
                _Xbox = new Xbox(_Logger);
                _Xbox.Connect(IPAddress.Parse(ipAddress));

                _Xbox.NotificationReceived += (sender, e) =>
                {
                    HandleNotification("notification", e.Message);
                };
            }
            catch (Exception ex)
            {
                Cleanup();
                error = ex.Message;
                return false;
            }

            return true;
        }

        private static Command.Response Connect(Command.Request request)
        {
            if (XboxConnected())
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

            if (!ConnectRaw(ipAddress, out string? error))
            {
                return GenericError(request, error != null ? error : $"Failed to connect to Xbox at {ipAddress}");
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
                        if (ConnectRaw(xbox.Ip.ToString(), out string? error))
                        {
                            return GenericSuccess(request, "Xbox rebooted and reconnected successfully.");
                        }
                        else
                        {
                            return GenericError(
                                request, 
                                error != null ?
                                    error : 
                                    $"Rebooted Xbox found at {xbox.Ip} but failed to reconnect.");
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

            if (request.Payload is not Command.MemoryReadArgs args)
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

            if (request.Payload is not Command.MemoryWriteArgs args)
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

            if (request.Payload is not Command.MemoryDumpArgs args)
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
                return ArgsError(request, "threadId is required for registers command.");
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

        private static bool EnsureRemoteDirectories(string remotePath, out string? error)
        {
            error = null;

            if (!XboxConnected())
            {
                error = "Not connected to any Xbox console.";
                return false;
            }

            try
            {
                var directoryPath = Path.GetDirectoryName(remotePath.Replace('\\', '/'));

                if (string.IsNullOrEmpty(directoryPath))
                {
                    error = "Empty directory path";
                    return false;
                }

                var rootPath = Path.GetPathRoot(directoryPath);
                if (string.IsNullOrEmpty(rootPath))
                {
                    error = "Invalid remote path.";
                    return false;
                }

                var currentPath = rootPath.TrimEnd('\\');
                var parts = directoryPath.Substring(
                    rootPath.Length).Split(
                        new[] { '/', '\\' }, 
                        StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    currentPath = $"{currentPath}\\{part}";
                    _Xbox.FileSystem.CreateDirectory(currentPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool UploadFileRaw(string localPath, string remotePath, out string? error)
        {
            if (!XboxConnected())
            {
                error = "Not connected to any Xbox console.";
                return false;
            }
            else if (!File.Exists(localPath))
            {
                error = "Local file not found.";
                return false;
            }
            else if (!EnsureRemoteDirectories(remotePath, out string? dirError))
            {
                error = dirError;
                return false;
            }

            try
            {
                _Xbox.FileSystem.UploadFile(localPath, remotePath);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static Command.Response UploadFiles(Command.Request request)
        {
            if (!XboxConnected())
            {
                return NotConnectedError(request);
            }

            if (request.Payload is not Command.UploadDownloadArgs args)
            {
                return ArgsError(request);
            }

            var results = new List<object>();

            foreach (var path in args.Paths)
            {
                if (Directory.Exists(path.LocalPath))
                {
                    var allFiles = Directory.GetFiles(path.LocalPath, "*.*", SearchOption.AllDirectories);

                    foreach (var file in allFiles)
                    {
                        var relativePath = Path.GetRelativePath(path.LocalPath, file);
                        var remoteFilePath = Path.Combine(path.RemotePath, relativePath);

                        if (!UploadFileRaw(file, remoteFilePath, out string? error))
                        {
                            results.Add(new
                            {
                                success = false,
                                localPath = file,
                                remotePath = remoteFilePath,
                                message = error ?? "Failed to upload file."
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                success = true,
                                localPath = file,
                                remotePath = remoteFilePath,
                                message = ""
                            });
                        }
                    }
                }
                else if (!UploadFileRaw(path.LocalPath, path.RemotePath, out string? error))
                {
                    results.Add(new
                    {
                        success = false,
                        localPath = path.LocalPath,
                        remotePath = path.RemotePath,
                        message = error ?? "Failed to upload file."
                    });
                }
                else
                {
                    results.Add(new
                    {
                        success = true,
                        localPath = path.LocalPath,
                        remotePath = path.RemotePath,
                        message = ""
                    });
                }
            }

            if (results.Count == 0 ||
                !results.Any(r => r.GetType().GetProperty("success")?.GetValue(r) is true))
            {
                return GenericError(request, "No files uploaded.");
            }

            return new Command.Response(request.Type, true, null, results);
        }

        private static Command.Response Launch(Command.Request request)
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

        private static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("==== Available commands ====");
            var consoleLimit = 64;

            foreach (var desc in Command.Help.Descriptions)
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
                WriteWrapped(desc.Desc, 4, consoleLimit);

                if (desc.Args != null)
                {
                    WriteWrapped("Params:", 4, consoleLimit);

                    foreach (var arg in desc.Args)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        WriteWrapped(arg.Arg, 6, consoleLimit);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        WriteWrapped(arg.Desc, 8, consoleLimit);
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
            Console.ResetColor();
        }

        private static void Cleanup()
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

        private static void PrintObject(object obj, int indent = 1)
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

        private static void HandleNotification(string type, string messageStr)
        {
            if (_NotificationsMuted)
            {
                return;
            }

            _XboxNotifications.Enqueue(new XboxNotification
            {
                Type = type,
                Timestamp = DateTime.UtcNow.ToString(),
                Message = messageStr
            });
        }
    }
}
