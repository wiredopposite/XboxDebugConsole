using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxDebugConsole.Command
{
    internal static class Parser
    {
        public static bool TryParse(string input, out Request request, out string? error)
        {
            request = new Request(Type.Unknown, null);
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Input cannot be empty.";
                return false;
            }

            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string commandStr = parts[0].ToLower();

            Type? type = EnumExtensions.GetValueFromDescription<Type>(commandStr);
            if (!type.HasValue || type == Type.Count || type == Type.Unknown)
            {
                error = "Unknown command.";
                return false;
            }

            string[] args = parts.Skip(1).ToArray();

            RequestFactory.Args reqArgs = new RequestFactory.Args
            {
                Ip             = TryParseString("ip", args),
                Name           = TryParseString("name", args),
                LocalPath      = TryParseString("localPath", args),
                RemotePath     = TryParseString("remotePath", args),
                PdbPath        = TryParseString("pdbPath", args),
                File           = TryParseString("file", args),
                Address        = TryParseHex(TryParseString("address", args)),
                ImageBase      = TryParseHex(TryParseString("imageBase", args)),
                TimeoutMs      = TryParseInt("timeoutMs", args),
                Length         = TryParseInt("length", args),
                ThreadId       = TryParseInt("threadId", args),
                Data           = TryParseHexBytes(TryParseString("data", args)),
                AutoReconnect  = TryParseBool("autoReconnect", args),
                Breakpoints    = null,
                UpDown         = null
            };

            int? line = TryParseInt("line", args);

            if ((reqArgs.File != null && line != null) || reqArgs.Address != null)
            {
                reqArgs.Breakpoints = new BreakpointArgs(new[] {
                    new BreakpointSpec(
                        Address: reqArgs.Address,
                        File: reqArgs.File ?? "",
                        Line: line ?? -1)
                });
            }

            if (reqArgs.LocalPath != null && reqArgs.RemotePath != null)
            {
                reqArgs.UpDown = new UploadDownloadArgs(new[] {
                    new LocalRemotePair(reqArgs.LocalPath, reqArgs.RemotePath)
                });
            }

            if (!RequestFactory.Create(type.Value, reqArgs, out request, out error))
            {
                if (error == null)
                    error = "Failed to create request.";

                return false;
            }

            return true;
        }

        private static string? TryParseString(string subcommand, string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(subcommand + "=", StringComparison.OrdinalIgnoreCase))
                {
                    string value = args[i].Substring(subcommand.Length + 1);

                    if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    return value;
                }
            }

            return null;
        }

        private static uint? TryParseUint(string subcommand, string[] args)
        {
            string? strValue = TryParseString(subcommand, args);
            if (strValue != null && uint.TryParse(strValue, out uint uintValue))
            {
                return uintValue;
            }
            return null;
        }

        private static int? TryParseInt(string subcommand, string[] args)
        {
            string? strValue = TryParseString(subcommand, args);
            if (strValue != null && int.TryParse(strValue, out int intValue))
            {
                return intValue;
            }
            return null;
        }

        private static bool? TryParseBool(string subcommand, string[] args)
        {
            string? strValue = TryParseString(subcommand, args);
            if (strValue != null && bool.TryParse(strValue, out bool boolValue))
            {
                return boolValue;
            }
            return null;
        }

        private static uint? TryParseHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var cleaned = value.Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[2..];

            if (uint.TryParse(cleaned, System.Globalization.NumberStyles.HexNumber, null, out var result))
                return result;

            if (uint.TryParse(cleaned, out result))
                return result;

            return null;
        }

        private static byte[]? TryParseHexBytes(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return null;

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

            if (bytes.Count == 0)
                return null;

            return bytes.ToArray();
        }
    }
}
