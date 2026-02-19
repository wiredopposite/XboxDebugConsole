using Newtonsoft.Json.Linq;

namespace XboxDebugConsole
{
    internal static class JsonCommandParser
    {
        public static bool TryParse(string input, out CommandRequest request, out string? error)
        {
            request = new CommandRequest(Command.Unknown, null);
            error = null;

            JObject payload;
            try
            {
                payload = JObject.Parse(input);
            }
            catch
            {
                error = "Invalid JSON input.";
                return false;
            }

            var commandStr = payload["command"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(commandStr))
            {
                error = "Command field is required.";
                return false;
            }

            Command? command = EnumExtensions.GetValueFromDescription<Command>(commandStr.ToLowerInvariant());
            if (!command.HasValue)
            {
                error = "Unknown command.";
                return false;
            }

            request = command.Value switch
            {
                Command.Scan => CommandRequest.From(
                    command.Value, 
                    new ScanArgs(ReadInt(payload["timeoutMs"], 5000))),
                Command.Connect => CommandRequest.From(
                    command.Value,
                    new ConnectArgs(
                        payload["ip"]?.ToString(),
                        payload["name"]?.ToString(),
                        ReadInt(payload["timeoutMs"], 5000))),
                Command.LoadSymbols => ParseLoadSymbols(
                    command.Value, 
                    payload, 
                    out error),
                Command.SetBreakpoint => CommandRequest.From(
                    command.Value, 
                    ParseBreakpoints(payload)),
                Command.DeleteBreakpoint => CommandRequest.From(
                    command.Value, 
                    ParseBreakpoints(payload)),
                Command.ReadMemory => CommandRequest.From(
                    command.Value,
                    new MemoryReadArgs(
                        payload["address"]?.ToString() ?? string.Empty,
                        ReadInt(payload["length"], 0))),
                Command.DumpMemory => CommandRequest.From(
                    command.Value,
                    new MemoryDumpArgs(
                        payload["address"]?.ToString() ?? string.Empty,
                        ReadInt(payload["length"], 0),
                        payload["localPath"]?.ToString() ?? string.Empty)),
                Command.WriteMemory => CommandRequest.From(
                    command.Value,
                    new MemoryWriteArgs(
                        payload["address"]?.ToString() ?? string.Empty,
                        payload["data"]?.ToString() ?? string.Empty)),
                Command.Registers => CommandRequest.From(command.Value, 
                    new ThreadArgs(ReadInt(payload["threadId"], 0))),
                Command.Upload => CommandRequest.From(command.Value,
                    new UploadArgs(
                        payload["localPath"]?.ToString() ?? string.Empty,
                        payload["remotePath"]?.ToString() ?? string.Empty)),
                Command.Launch => CommandRequest.From(command.Value,
                    new LaunchArgs(
                        payload["remotePath"]?.ToString() ?? string.Empty)),
                Command.Reboot => CommandRequest.From(command.Value,
                    new RebootArgs(
                        ReadBool(payload["autoReconnect"], false),
                        ReadInt(payload["timeoutMs"], 10000))),
                _ => new CommandRequest(command.Value, null)
            };

            if (error != null)
                return false;

            return true;
        }

        private static CommandRequest ParseLoadSymbols(Command command, JObject payload, out string? error)
        {
            error = null;
            var pdbPath = payload["pdbPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(pdbPath))
            {
                error = "pdbPath is required for loadSymbols command.";
                return new CommandRequest(command, null);
            }

            var imageBase = ReadUInt(payload["imageBase"]);
            return CommandRequest.From(command, new LoadSymbolsArgs(pdbPath, imageBase));
        }

        private static BreakpointArgs ParseBreakpoints(JObject payload)
        {
            var breakpoints = new List<BreakpointSpec>();

            if (payload["breakpoints"] is JArray array)
            {
                foreach (var token in array.OfType<JObject>())
                {
                    breakpoints.Add(ParseBreakpoint(token));
                }
            }
            else
            {
                breakpoints.Add(ParseBreakpoint(payload));
            }

            return new BreakpointArgs(breakpoints);
        }

        private static BreakpointSpec ParseBreakpoint(JObject payload)
        {
            return new BreakpointSpec(
                Address: payload["address"]?.ToString(),
                File: payload["file"]?.ToString(),
                Line: ReadNullableInt(payload["line"]));
        }

        private static int ReadInt(JToken? token, int fallback)
        {
            if (token == null)
                return fallback;

            if (token.Type == JTokenType.Integer)
                return token.Value<int>();

            if (int.TryParse(token.ToString(), out var value))
                return value;

            return fallback;
        }

        private static int? ReadNullableInt(JToken? token)
        {
            if (token == null)
                return null;

            if (token.Type == JTokenType.Integer)
                return token.Value<int>();

            if (int.TryParse(token.ToString(), out var value))
                return value;

            return null;
        }

        private static bool ReadBool(JToken? token, bool fallback)
        {
            if (token == null)
                return fallback;

            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            if (bool.TryParse(token.ToString(), out var value))
                return value;

            return fallback;
        }

        private static uint? ReadUInt(JToken? token)
        {
            if (token == null)
                return null;

            if (token.Type == JTokenType.Integer)
                return token.Value<uint>();

            var str = token.ToString().Trim();
            if (string.IsNullOrWhiteSpace(str))
                return null;

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(str[2..], System.Globalization.NumberStyles.HexNumber, null, out var hexValue))
                    return hexValue;
                return null;
            }

            if (uint.TryParse(str, out var value))
                return value;

            return null;
        }
    }
}
