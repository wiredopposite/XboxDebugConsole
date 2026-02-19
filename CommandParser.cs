using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxDebugConsole
{
    internal static class CommandParser
    {
        public static bool TryParse(string input, out CommandRequest request, out string? error)
        {
            request = new CommandRequest(Command.Unknown, null);
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Input cannot be empty.";
                return false;
            }

            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string commandStr = parts[0].ToLower();

            Command? command = EnumExtensions.GetValueFromDescription<Command>(commandStr);
            if (!command.HasValue)
            {
                error = "Unknown command.";
                return false;
            }

            string[] args = parts.Skip(1).ToArray();

            string? ipAddress   = ParseStringNullable("ip", args);
            string? localPath   = ParseStringNullable("localPath", args);
            string? pdbPath     = ParseStringNullable("pdbPath", args);
            string? remotePath  = ParseStringNullable("remotePath", args);
            string? address     = ParseStringNullable("address", args);
            string? data        = ParseStringNullable("data", args);
            string? xboxName    = ParseStringNullable("name", args);

            int timeoutMs   = ParseInt("timeoutMs", args, 5000);
            int length      = ParseInt("length", args, 0);
            int threadId    = ParseInt("threadId", args, 0);

            bool autoReconnect = ParseBool("autoReconnect", args, true);

            request = BuildCommandRequestFromArgs(
                command.Value,
                ipAddress, xboxName, localPath, remotePath, pdbPath,
                address, data, timeoutMs, length,
                threadId, autoReconnect
                );

            return true;
        }

        private static CommandRequest BuildCommandRequestFromArgs(
            Command command, string? ipAddress, string? xboxName,
            string? localPath, string? remotePath, string? pdbPath, string? address, string? data,
            int timeoutMs, int length, int threadId, bool autoReconnect)
        {
            return command switch
            {
                Command.Scan => CommandRequest.From(command, new ScanArgs(timeoutMs)),
                Command.Connect => CommandRequest.From(command, new ConnectArgs(ipAddress, xboxName, timeoutMs)),
                Command.LoadSymbols when !string.IsNullOrWhiteSpace(pdbPath) => CommandRequest.From(command, new LoadSymbolsArgs(pdbPath)),
                Command.SetBreakpoint => CommandRequest.From(command, new BreakpointArgs(new[] { new BreakpointSpec(Address: address) })),
                Command.DeleteBreakpoint => CommandRequest.From(command, new BreakpointArgs(new[] { new BreakpointSpec(Address: address) })),
                Command.ReadMemory => CommandRequest.From(command, new MemoryReadArgs(address ?? string.Empty, length)),
                Command.DumpMemory => CommandRequest.From(command, new MemoryDumpArgs(address ?? string.Empty, length, localPath ?? string.Empty)),
                Command.WriteMemory => CommandRequest.From(command, new MemoryWriteArgs(address ?? string.Empty, data ?? string.Empty)),
                Command.Registers => CommandRequest.From(command, new ThreadArgs(threadId)),
                Command.Upload => CommandRequest.From(command, new UploadArgs(localPath ?? string.Empty, remotePath ?? string.Empty)),
                Command.Launch => CommandRequest.From(command, new LaunchArgs(remotePath ?? string.Empty)),
                Command.Reboot => CommandRequest.From(command, new RebootArgs(autoReconnect, timeoutMs)),
                _ => new CommandRequest(command, null)
            };
        }

        private static string? ParseStringNullable(string subcommand, string[] args)
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

        private static int ParseInt(string subcommand, string[] args, int defaultValue = 0)
        {
            string? strValue = ParseStringNullable(subcommand, args);
            if (strValue != null && int.TryParse(strValue, out int intValue))
            {
                return intValue;
            }
            return defaultValue;
        }

        private static bool ParseBool(string subcommand, string[] args, bool defaultValue = false)
        {
            string? strValue = ParseStringNullable(subcommand, args);
            if (strValue != null && bool.TryParse(strValue, out bool boolValue))
            {
                return boolValue;
            }
            return defaultValue;
        }
    }
}
