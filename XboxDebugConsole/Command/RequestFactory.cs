using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxDebugConsole.Command
{
    internal class RequestFactory
    {
        internal class Args
        {
            public string? Ip { get; set; } = null;
            public string? Name { get; set; } = null;
            public string? LocalPath { get; set; } = null;
            public string? RemotePath { get; set; } = null;
            public string? PdbPath { get; set; } = null;
            public string? File { get; set; } = null;
            public uint? Address { get; set; } = null;
            public uint? ImageBase { get; set; } = null;
            public int? TimeoutMs { get; set; } = null;
            public int? Length { get; set; } = null;
            public int? ThreadId { get; set; } = null;
            public byte[]? Data { get; set; } = null;
            public bool? AutoReconnect { get; set; } = null;
            public BreakpointArgs? Breakpoints { get; set; } = null;
            public UploadDownloadArgs? UpDown { get; set; } = null;
        }

        public static bool Create(Type type, Args args, out Request request, out string? error)
        {
            error = null;
            request = new Request(Type.Unknown, null);

            switch (type)
            {
                case Type.Scan:
                    request = Request.From(
                        type,
                        new ScanArgs(args.TimeoutMs ?? 5000));
                    break;

                case Type.Connect:
                    request = Request.From(
                        type,
                        new ConnectArgs(
                            args.Ip,
                            args.Name,
                            args.TimeoutMs ?? 5000));
                    break;

                case Type.Reboot:
                    request = Request.From(
                        type,
                        new RebootArgs(
                            args.AutoReconnect ?? false,
                            args.TimeoutMs ?? 10000));
                    break;

                case Type.LoadSymbols:
                    if (string.IsNullOrWhiteSpace(args.PdbPath))
                    {
                        error = "pdbPath is required for LoadSymbols command.";
                        return false;
                    }
                    else if (args.ImageBase == null)
                    {
                        error = "imageBase is required for LoadSymbols command.";
                        return false;
                    }

                    request = Request.From(
                        type,
                        new LoadSymbolsArgs(args.PdbPath, args.ImageBase));
                    break;

                case Type.Functions:
                    request = new Request(type, new FunctionArgs(args.File));
                    break;

                case Type.DeleteBreakpoint:
                case Type.SetBreakpoint:
                    if (args.Breakpoints == null)
                    {
                        error = "Either file and line or address is required for SetBreakpoint command.";
                        return false;
                    }

                    request = Request.From(type, args.Breakpoints);
                    break;

                case Type.ReadMemory:
                    if (args.Address == null || args.Length == null || args.Length.Value < 1)
                    {
                        error = "address and length are required for ReadMemory command.";
                        return false;
                    }

                    request = Request.From(
                        type,
                        new MemoryReadArgs(args.Address.Value, args.Length.Value));
                    break;

                case Type.DumpMemory:
                    if (args.Address == null || args.Length == null ||
                        args.Length.Value < 1 || string.IsNullOrWhiteSpace(args.LocalPath))
                    {
                        error = "address, length and localPath are required for DumpMemory command.";
                        return false;
                    }

                    request = Request.From(
                        type,
                        new MemoryDumpArgs(args.Address.Value, args.Length.Value, args.LocalPath));
                    break;

                case Type.WriteMemory:
                    if (args.Address == null || args.Length == null || args.Data == null)
                    {
                        error = "address and data are required for WriteMemory command.";
                        return false;
                    }

                    request = Request.From(
                        type,
                        new MemoryWriteArgs(
                            args.Address.Value,
                            args.Data));
                    break;

                case Type.Registers:
                    request = Request.From(
                        type,
                        new ThreadArgs(args.ThreadId));
                    break;

                case Type.Upload:
                case Type.Download:
                    if (args.UpDown == null)
                    {
                        error = "localPath and remotePath are required for Upload command.";
                        return false;
                    }

                    request = Request.From(
                        type,
                        args.UpDown);
                    break;

                case Type.Launch:
                    if (string.IsNullOrWhiteSpace(args.RemotePath))
                    {
                        error = "remotePath is required for Launch command.";
                        return false;
                    }

                    request = Request.From(
                        type,
                        new LaunchArgs(args.RemotePath));
                    break;

                //case Type.Mute:
                //case Type.Unmute:
                //case Type.Pause:
                //case Type.Resume:
                //case Type.Disconnect:
                default:
                    request = new Request(type, null);
                    break;
            }

            return true;
        }
    }
}
