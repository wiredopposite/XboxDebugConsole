using Newtonsoft.Json.Linq;

namespace XboxDebugConsole
{
    namespace Command
    {
        internal static class ParserJson
        {
            public static bool TryParse(string input, out Request request, out string? error)
            {
                request = new Request(Type.Unknown, null);
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

                Type? cmdType = EnumExtensions.GetValueFromDescription<Type>(commandStr.ToLowerInvariant());
                if (!cmdType.HasValue)
                {
                    error = "Unknown command.";
                    return false;
                }

                request = cmdType.Value switch
                {
                    Type.Scan => Request.From(
                        cmdType.Value,
                        new ScanArgs(ReadInt(payload["timeoutMs"], 5000))),
                    Type.Connect => Request.From(
                        cmdType.Value,
                        new ConnectArgs(
                            payload["ip"]?.ToString(),
                            payload["name"]?.ToString(),
                            ReadInt(payload["timeoutMs"], 5000))),
                    Type.LoadSymbols => ParseLoadSymbols(
                        cmdType.Value,
                        payload,
                        out error),
                    Type.SetBreakpoint => Request.From(
                        cmdType.Value,
                        ParseBreakpoints(payload)),
                    Type.DeleteBreakpoint => Request.From(
                        cmdType.Value,
                        ParseBreakpoints(payload)),
                    Type.ReadMemory => Request.From(
                        cmdType.Value,
                        new MemoryReadArgs(
                            ReadUInt(payload["address"]) ?? 0,
                            ReadInt(payload["length"], 0))),
                    Type.DumpMemory => Request.From(
                        cmdType.Value,
                        new MemoryDumpArgs(
                            ReadUInt(payload["address"]) ?? 0,
                            ReadInt(payload["length"], 0),
                            payload["localPath"]?.ToString() ?? string.Empty)),
                    Type.WriteMemory => Request.From(
                        cmdType.Value,
                        new MemoryWriteArgs(
                            ReadUInt(payload["address"]) ?? 0,
                            ReadBytes(payload["data"]))),
                    Type.Registers => Request.From(cmdType.Value,
                        new ThreadArgs(ReadInt(payload["threadId"], 0))),
                    Type.Upload => Request.From(cmdType.Value,
                        new UploadArgs(
                            payload["localPath"]?.ToString() ?? string.Empty,
                            payload["remotePath"]?.ToString() ?? string.Empty)),
                    Type.Launch => Request.From(cmdType.Value,
                        new LaunchArgs(
                            payload["remotePath"]?.ToString() ?? string.Empty)),
                    Type.Reboot => Request.From(cmdType.Value,
                        new RebootArgs(
                            ReadBool(payload["autoReconnect"], false),
                            ReadInt(payload["timeoutMs"], 10000))),
                    _ => new Request(cmdType.Value, null)
                };

                if (error != null)
                    return false;

                return true;
            }

            private static Request ParseLoadSymbols(Type type, JObject payload, out string? error)
            {
                error = null;
                var pdbPath = payload["pdbPath"]?.ToString();
                if (string.IsNullOrWhiteSpace(pdbPath))
                {
                    error = "pdbPath is required for loadSymbols command.";
                    return new Request(type, null);
                }

                var imageBase = ReadUInt(payload["imageBase"]);
                return Request.From(type, new LoadSymbolsArgs(pdbPath, imageBase));
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
                    Address: ReadUInt(payload["address"]),
                    File: payload["file"]?.ToString() ?? "",
                    Line: ReadNullableInt(payload["line"]) ?? -1);
            }

            private static byte[] ReadBytes(JToken? token)
            {
                if (token == null)
                    return Array.Empty<byte>();

                if (token.Type == JTokenType.Array)
                {
                    var bytes = new List<byte>();
                    foreach (var item in token.Children())
                    {
                        if (item.Type == JTokenType.Integer)
                        {
                            bytes.Add((byte)item.Value<int>());
                        }
                        else if (byte.TryParse(item.ToString(), out var value))
                        {
                            bytes.Add(value);
                        }
                    }

                    return bytes.ToArray();
                }

                var data = token.ToString();
                return ParseHexBytes(data);
            }

            private static byte[] ParseHexBytes(string data)
            {
                if (string.IsNullOrWhiteSpace(data))
                    return Array.Empty<byte>();

                var parts = data.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var bytes = new List<byte>();

                foreach (var part in parts)
                {
                    var cleaned = part.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? part[2..]
                        : part;

                    if (byte.TryParse(cleaned, System.Globalization.NumberStyles.HexNumber, null, out var value))
                        bytes.Add(value);
                }

                return bytes.ToArray();
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
}
