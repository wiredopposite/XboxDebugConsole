using Newtonsoft.Json.Linq;

namespace XboxDebugConsole.Command
{
    internal static class ParserJson
    {
        public static bool TryParse(string input, out Request request, out string? error)
        {
            request = new Request(Type.Unknown, null);
            error = null;

            JObject jsonCmd;
            try
            {
                jsonCmd = JObject.Parse(input);
            }
            catch
            {
                error = "Invalid JSON input.";
                return false;
            }

            var cmdtypeStr = jsonCmd["type"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(cmdtypeStr))
            {
                error = "Command field is required.";
                return false;
            }

            Type? cmdType = EnumExtensions.GetValueFromDescription<Type>(cmdtypeStr.ToLowerInvariant());
            if (!cmdType.HasValue)
            {
                error = "Unknown command.";
                return false;
            }

            RequestFactory.Args reqArgs = new RequestFactory.Args
            {
                Ip              = jsonCmd["ip"]?.ToString(),
                Name            = jsonCmd["name"]?.ToString(),
                LocalPath       = jsonCmd["localPath"]?.ToString(),
                RemotePath      = jsonCmd["remotePath"]?.ToString(),
                PdbPath         = jsonCmd["pdbPath"]?.ToString(),
                File            = jsonCmd["file"]?.ToString(),
                Address         = TryReadUInt(jsonCmd["address"]),
                ImageBase       = TryReadUInt(jsonCmd["imageBase"]),
                TimeoutMs       = TryReadInt(jsonCmd["timeoutMs"]),
                Length          = TryReadInt(jsonCmd["length"]),
                ThreadId        = TryReadInt(jsonCmd["threadId"]),
                Data            = TryReadBytes(jsonCmd["data"]),
                AutoReconnect   = TryReadBool(jsonCmd["autoReconnect"]),
                Breakpoints     = TryParseBreakpoints(jsonCmd),
                UpDown          = TryParseUpDown(jsonCmd)
            };

            if (!RequestFactory.Create(cmdType.Value, reqArgs, out request, out error))
            {
                if (error == null)
                {
                    error = "Failed to create request.";
                }
                return false;
            }

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

            var imageBase = TryReadUInt(payload["imageBase"]);
            return Request.From(type, new LoadSymbolsArgs(pdbPath, imageBase));
        }

        private static UploadDownloadArgs? TryParseUpDown(JObject payload)
        {
            var pairs = new List<LocalRemotePair>();

            if (payload["files"] is JArray array)
            {
                foreach (var token in array.OfType<JObject>())
                {
                    var local = token["localPath"]?.ToString();
                    var remote = token["remotePath"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(local) && !string.IsNullOrWhiteSpace(remote))
                    {
                        pairs.Add(new LocalRemotePair(local, remote));
                    }
                }
            }
            else
            {
                var local = payload["localPath"]?.ToString();
                var remote = payload["remotePath"]?.ToString();

                if (!string.IsNullOrWhiteSpace(local) && !string.IsNullOrWhiteSpace(remote))
                {
                    pairs.Add(new LocalRemotePair(local, remote));
                }
            }

            if (pairs.Count == 0)
                return null;

            return new UploadDownloadArgs(pairs);
        }

        private static BreakpointArgs? TryParseBreakpoints(JObject payload)
        {
            var breakpoints = new List<BreakpointSpec>();
            BreakpointSpec? bp;

            if (payload["breakpoints"] is JArray array)
            {
                foreach (var token in array.OfType<JObject>())
                {
                    bp = TryParseBreakpoint(token);

                    if (bp != null)
                    {
                        breakpoints.Add(bp);
                    }
                }
            }
            else
            {
                bp = TryParseBreakpoint(payload);

                if (bp != null)
                {
                    breakpoints.Add(bp);
                }
            }

            if (breakpoints.Count == 0)
                return null;

            return new BreakpointArgs(breakpoints);
        }

        private static BreakpointSpec? TryParseBreakpoint(JObject payload)
        {
            uint? address = TryReadUInt(payload["address"]);
            string? file = payload["file"]?.ToString();
            int? line = TryReadInt(payload["line"]);

            if ((file == null || line == null) && address == null)
            {
                return null;
            }

            return new BreakpointSpec(
                Address: address,
                File: file ?? "",
                Line: line ?? -1);
        }

        private static byte[]? TryReadBytes(JToken? token)
        {
            if (token == null)
                return null;

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

                if (bytes.Count == 0)
                    return null;

                return bytes.ToArray();
            }

            var data = token.ToString();
            byte[] byteArr = ParseHexBytes(data);
            return byteArr.Length > 0 ? byteArr : null;
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

        private static int? TryReadInt(JToken? token)
        {
            if (token == null)
                return null;

            if (token.Type == JTokenType.Integer)
                return token.Value<int>();

            if (int.TryParse(token.ToString(), out var value))
                return value;

            return null;
        }

        private static bool? TryReadBool(JToken? token)
        {
            if (token == null)
                return null;

            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            if (bool.TryParse(token.ToString(), out var value))
                return value;

            return null;
        }

        private static uint? TryReadUInt(JToken? token)
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
