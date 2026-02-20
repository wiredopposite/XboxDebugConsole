using System.Runtime.InteropServices;
using Dia2Lib;

namespace XboxDebugConsole
{
    class LineMapping
    {
        public required string File { get; set; }
        public int Line { get; set; }
        public uint Address { get; set; }
    }

    class SourceLocation
    {
        public required string File { get; set; }
        public int Line { get; set; }
        public required string Function { get; set; }
    }

    class LocalVariableInfo
    {
        public required string Name { get; set; }
        public required string TypeName { get; set; }
        public uint Size { get; set; }
        public uint LocationType { get; set; }
        public uint RegisterId { get; set; }
        public int Offset { get; set; }
        public uint? Address { get; set; }
        public bool IsParameter { get; set; }
    }

    class FunctionInfo
    {
        public required string Name { get; set; }
        public uint Rva { get; set; }
        public uint Address { get; set; }
        public uint Length { get; set; }
    }

    internal class SymbolManager
    {
        private enum DiaDataKind : uint
        {
            DataIsUnknown = 0,
            DataIsLocal = 1,
            DataIsStaticLocal = 2,
            DataIsParam = 3,
            DataIsObjectPtr = 4,
            DataIsFileStatic = 5,
            DataIsGlobal = 6,
            DataIsMember = 7,
            DataIsStaticMember = 8,
            DataIsConstant = 9
        }

        private enum DiaLocationType : uint
        {
            LocIsNull = 0,
            LocIsStatic = 1,
            LocIsTLS = 2,
            LocIsRegRel = 3,
            LocIsThisRel = 4,
            LocIsEnregistered = 5,
            LocIsBitField = 6,
            LocIsSlot = 7,
            LocIsIlRel = 8,
            LocInMetaData = 9,
            LocIsConstant = 10
        }

        private enum DiaBasicType : uint
        {
            btNoType = 0,
            btVoid = 1,
            btChar = 2,
            btWChar = 3,
            btInt = 6,
            btUInt = 7,
            btFloat = 8,
            btBool = 10,
            btLong = 13,
            btULong = 14
        }

        private readonly Dictionary<string, List<LineMapping>> _FileToAddresses = new(StringComparer.OrdinalIgnoreCase);
        private readonly SortedDictionary<uint, SourceLocation> _AddressesToSource = new();
        private IDiaDataSource? _diaDataSource;
        private IDiaSession? _diaSession;
        private uint _imageBase;

        public bool Initialized()
        {
            return _diaSession != null;
        }

        public bool LoadPdb(string pdbPath)
        {
            if (!File.Exists(pdbPath))
            {
                return false;
            }

            try
            {
                _diaDataSource = new DiaSource();
                _diaDataSource.loadDataFromPdb(pdbPath);
                _diaDataSource.openSession(out _diaSession);

                if (_diaSession == null)
                {
                    return false;
                }

                LoadSymbolsAndLines();
                return true;
            }
            catch //COMException ex)
            {
                return false;
            }
            //catch (Exception ex)
            //{
            //    return false;
            //}
        }

        private void LoadSymbolsAndLines()
        {
            if (_diaSession == null) return;

            try
            {
                IDiaSymbol globalScope = _diaSession.globalScope;

                IDiaEnumSymbols? enumSymbols = null;
                globalScope.findChildren(SymTagEnum.SymTagFunction, null, 0, out enumSymbols);

                if (enumSymbols == null) return;

                IDiaSymbol? symbol;
                uint celt = 0;

                while (true)
                {
                    enumSymbols.Next(1, out symbol, out celt);
                    if (celt != 1 || symbol == null)
                        break;

                    ProcessFunction(symbol);
                    if (OperatingSystem.IsWindows())
                        Marshal.ReleaseComObject(symbol);
                }

                if (OperatingSystem.IsWindows())
                {
                    Marshal.ReleaseComObject(enumSymbols);
                    Marshal.ReleaseComObject(globalScope);
                }
            }
            catch //(COMException ex)
            {
                //Console.WriteLine($"Error loading symbols: {ex.Message}");
            }
        }

        private void ProcessFunction(IDiaSymbol function)
        {
            try
            {
                string functionName = function.name ?? "Unknown";
                uint functionRva = function.relativeVirtualAddress;

                IDiaEnumLineNumbers? enumLineNumbers = null;
                _diaSession?.findLinesByRVA(functionRva, (uint)function.length, out enumLineNumbers);

                if (enumLineNumbers == null) return;

                IDiaLineNumber? lineNumber;
                uint celt = 0;

                while (true)
                {
                    enumLineNumbers.Next(1, out lineNumber, out celt);
                    if (celt != 1 || lineNumber == null)
                        break;

                    IDiaSourceFile sourceFile = lineNumber.sourceFile;
                    string fileName = sourceFile.fileName ?? "Unknown";
                    string fileKey = NormalizeFileKey(fileName);
                    uint lineNum = lineNumber.lineNumber;
                    uint address = lineNumber.relativeVirtualAddress;

                    if (!_FileToAddresses.ContainsKey(fileKey))
                        _FileToAddresses[fileKey] = new List<LineMapping>();

                    _FileToAddresses[fileKey].Add(new LineMapping
                    {
                        File = fileName,
                        Line = (int)lineNum,
                        Address = address
                    });

                    _AddressesToSource[address] = new SourceLocation
                    {
                        File = fileName,
                        Line = (int)lineNum,
                        Function = functionName
                    };

                    if (OperatingSystem.IsWindows())
                    {
                        Marshal.ReleaseComObject(sourceFile);
                        Marshal.ReleaseComObject(lineNumber);
                    }
                }

                if (OperatingSystem.IsWindows())
                    Marshal.ReleaseComObject(enumLineNumbers);
            }
            catch (COMException ex)
            {
                //Console.WriteLine($"Error processing function: {ex.Message}");
            }
        }

        public void SetImageBase(uint imageBase)
        {
            _imageBase = imageBase;
        }

        public uint? GetAddressForLine(string file, int line)
        {           
            var fileKey = NormalizeFileKey(file);
            if (!_FileToAddresses.TryGetValue(fileKey, out var mappings))
                return null;

            var address = mappings.FirstOrDefault(m => m.Line == line)?.Address;
            return address.HasValue ? ToAbsolute(address.Value) : null;
        }

        public SourceLocation? GetSourceLocation(uint address)
        {
            var rva = ToRva(address);
            if (_AddressesToSource.TryGetValue(rva, out var loc))
                return loc;

            foreach (var pair in _AddressesToSource.Reverse())
            {
                if (pair.Key <= rva)
                    return pair.Value;
            }

            return null;
        }

        public IReadOnlyList<LocalVariableInfo> GetLocalsForAddress(uint address, uint frameBase,
            IReadOnlyDictionary<uint, uint>? registers = null)
        {
            if (_diaSession == null)
                return Array.Empty<LocalVariableInfo>();

            var rva = ToRva(address);
            IDiaSymbol? function = null;
            _diaSession.findSymbolByRVA(rva, SymTagEnum.SymTagFunction, out function);
            if (function == null)
                return Array.Empty<LocalVariableInfo>();

            var locals = new List<LocalVariableInfo>();
            IDiaEnumSymbols? enumSymbols = null;
            function.findChildren(SymTagEnum.SymTagData, null, 0, out enumSymbols);

            if (enumSymbols != null)
            {
                IDiaSymbol? symbol;
                uint celt = 0;

                while (true)
                {
                    enumSymbols.Next(1, out symbol, out celt);
                    if (celt != 1 || symbol == null)
                        break;

                    var dataKind = (DiaDataKind)symbol.dataKind;
                    if (dataKind == DiaDataKind.DataIsLocal || dataKind == DiaDataKind.DataIsParam)
                    {
                        var typeSymbol = symbol.type;
                        var info = new LocalVariableInfo
                        {
                            Name = symbol.name ?? "<unnamed>",
                            TypeName = GetTypeName(typeSymbol),
                            Size = (uint)(typeSymbol?.length ?? 0),
                            LocationType = symbol.locationType,
                            RegisterId = symbol.registerId,
                            Offset = symbol.offset,
                            IsParameter = dataKind == DiaDataKind.DataIsParam,
                            Address = ResolveAddress(symbol, frameBase, registers)
                        };

                        locals.Add(info);
                    }

                    if (OperatingSystem.IsWindows())
                    {
                        if (symbol.type != null)
                            Marshal.ReleaseComObject(symbol.type);
                        Marshal.ReleaseComObject(symbol);
                    }
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (enumSymbols != null)
                    Marshal.ReleaseComObject(enumSymbols);
                Marshal.ReleaseComObject(function);
            }

            return locals;
        }

        public IReadOnlyList<FunctionInfo> GetFunctions(string? fileFilter = null)
        {
            if (_diaSession == null)
                return Array.Empty<FunctionInfo>();

            var functions = new List<FunctionInfo>();
            var fileKey = string.IsNullOrWhiteSpace(fileFilter) ? null : NormalizeFileKey(fileFilter);
            IDiaSymbol globalScope = _diaSession.globalScope;
            IDiaEnumSymbols? enumSymbols = null;
            globalScope.findChildren(SymTagEnum.SymTagFunction, null, 0, out enumSymbols);

            if (enumSymbols != null)
            {
                IDiaSymbol? symbol;
                uint celt = 0;

                while (true)
                {
                    enumSymbols.Next(1, out symbol, out celt);
                    if (celt != 1 || symbol == null)
                        break;

                    if (fileKey != null && !FunctionMatchesFile(symbol, fileKey))
                    {
                        if (OperatingSystem.IsWindows())
                            Marshal.ReleaseComObject(symbol);
                        continue;
                    }

                    var rva = symbol.relativeVirtualAddress;
                    functions.Add(new FunctionInfo
                    {
                        Name = symbol.name ?? "Unknown",
                        Rva = rva,
                        Address = ToAbsolute(rva),
                        Length = (uint)symbol.length
                    });

                    if (OperatingSystem.IsWindows())
                        Marshal.ReleaseComObject(symbol);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (enumSymbols != null)
                    Marshal.ReleaseComObject(enumSymbols);
                Marshal.ReleaseComObject(globalScope);
            }

            return functions;
        }

        private bool FunctionMatchesFile(IDiaSymbol function, string fileKey)
        {
            IDiaEnumLineNumbers? enumLineNumbers = null;
            _diaSession?.findLinesByRVA(function.relativeVirtualAddress, (uint)function.length, out enumLineNumbers);

            if (enumLineNumbers == null)
                return false;

            IDiaLineNumber? lineNumber;
            uint celt = 0;
            var matched = false;

            while (true)
            {
                enumLineNumbers.Next(1, out lineNumber, out celt);
                if (celt != 1 || lineNumber == null)
                    break;

                var sourceFile = lineNumber.sourceFile;
                var name = sourceFile.fileName ?? string.Empty;
                if (NormalizeFileKey(name).Equals(fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                }

                if (OperatingSystem.IsWindows())
                {
                    Marshal.ReleaseComObject(sourceFile);
                    Marshal.ReleaseComObject(lineNumber);
                }

                if (matched)
                    break;
            }

            if (OperatingSystem.IsWindows())
                Marshal.ReleaseComObject(enumLineNumbers);

            return matched;
        }

        private uint? ResolveAddress(IDiaSymbol symbol, uint frameBase, IReadOnlyDictionary<uint, uint>? registers)
        {
            switch ((DiaLocationType)symbol.locationType)
            {
                case DiaLocationType.LocIsRegRel:
                case DiaLocationType.LocIsThisRel:
                    {
                        var baseAddress = ResolveRegisterBase(symbol.registerId, frameBase, registers);
                        return baseAddress + (uint)symbol.offset;
                    }
                case DiaLocationType.LocIsStatic:
                    return ToAbsolute(symbol.relativeVirtualAddress);
                default:
                    return null;
            }
        }

        private static uint ResolveRegisterBase(uint registerId, uint fallback, IReadOnlyDictionary<uint, uint>? registers)
        {
            if (registers != null && registers.TryGetValue(registerId, out var value))
                return value;

            return fallback;
        }

        private static string GetTypeName(IDiaSymbol? typeSymbol)
        {
            if (typeSymbol == null)
                return "unknown";

            if (!string.IsNullOrWhiteSpace(typeSymbol.name))
                return typeSymbol.name;

            return typeSymbol.baseType switch
            {
                (uint)DiaBasicType.btVoid => "void",
                (uint)DiaBasicType.btChar => "char",
                (uint)DiaBasicType.btWChar => "wchar_t",
                (uint)DiaBasicType.btInt => "int",
                (uint)DiaBasicType.btUInt => "uint",
                (uint)DiaBasicType.btFloat => "float",
                (uint)DiaBasicType.btBool => "bool",
                (uint)DiaBasicType.btLong => "long",
                (uint)DiaBasicType.btULong => "ulong",
                _ => "unknown"
            };
        }

        private static string NormalizeFileKey(string file)
        {
            var fileName = Path.GetFileName(file);
            return string.IsNullOrWhiteSpace(fileName) ? file : fileName;
        }

        private uint ToRva(uint address)
        {
            return _imageBase != 0 && address >= _imageBase ? address - _imageBase : address;
        }

        private uint ToAbsolute(uint rva)
        {
            return _imageBase != 0 ? rva + _imageBase : rva;
        }

        public void Dispose()
        {
            if (OperatingSystem.IsWindows())
            {
                if (_diaSession != null)
                {
                    Marshal.ReleaseComObject(_diaSession);
                    _diaSession = null;
                }

                if (_diaDataSource != null)
                {
                    Marshal.ReleaseComObject(_diaDataSource);
                    _diaDataSource = null;
                }
            }
        }

        ~SymbolManager()
        {
            Dispose();
        }
    }
}
