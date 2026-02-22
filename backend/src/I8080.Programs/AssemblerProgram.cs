using I8080.CpmSystem;

namespace I8080.Programs;

/// <summary>
/// ASM - Intel 8080 assembler. Reads .ASM files and produces .COM files.
/// Supports all 8080 mnemonics, labels, ORG, DB, DW, DS, EQU directives.
/// Two-pass assembler: pass 1 collects symbols, pass 2 generates code.
/// </summary>
public sealed class AssemblerProgram
{
    private readonly ITerminal _terminal;
    private readonly VirtualDisk _disk;

    // Symbol table
    private readonly Dictionary<string, ushort> _symbols = new(StringComparer.OrdinalIgnoreCase);
    // Output
    private byte[] _output = new byte[65536];
    private ushort _pc;
    private ushort _org = 0x0100;
    private ushort _minAddr;
    private ushort _maxAddr;
    private int _errors;
    private int _pass;
    private int _lineNum;

    public AssemblerProgram(ITerminal terminal, VirtualDisk disk)
    {
        _terminal = terminal;
        _disk = disk;
    }

    public void Run(string args)
    {
        var fileName = args.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(fileName))
        {
            _terminal.WriteLine("ASM - Intel 8080 Assembler");
            _terminal.WriteLine("Usage: ASM filename[.ASM]");
            return;
        }

        if (!fileName.Contains('.')) fileName += ".ASM";
        var source = _disk.ReadFileAsText(fileName);
        if (source == null)
        {
            _terminal.WriteLine($"File not found: {fileName}");
            return;
        }

        _terminal.WriteLine($"Assembling {fileName}...");
        var lines = source.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        _symbols.Clear();
        _output = new byte[65536];
        _errors = 0;

        // Pass 1: collect symbols
        _pass = 1;
        _pc = _org = 0x0100;
        _minAddr = 0xFFFF;
        _maxAddr = 0;
        foreach (var line in lines)
        {
            _lineNum++;
            AssembleLine(line);
        }

        // Pass 2: generate code
        _pass = 2;
        _pc = _org;
        _lineNum = 0;
        _minAddr = 0xFFFF;
        _maxAddr = 0;
        foreach (var line in lines)
        {
            _lineNum++;
            AssembleLine(line);
        }

        if (_errors > 0)
        {
            _terminal.WriteLine($"{_errors} error(s)");
            return;
        }

        // Write output .COM file
        var outName = fileName.Replace(".ASM", ".COM");
        int size = _maxAddr >= _minAddr ? _maxAddr - _minAddr + 1 : 0;
        if (size > 0 && _minAddr == 0x0100)
        {
            var comData = new byte[size];
            Array.Copy(_output, _minAddr, comData, 0, size);
            _disk.WriteFile(outName, comData);
            _terminal.WriteLine($"{outName}: {size} bytes, {_symbols.Count} symbols");

            // Also write .PRN listing
            WriteListing(fileName, lines);
        }
        else if (size > 0)
        {
            var comData = new byte[size];
            Array.Copy(_output, _minAddr, comData, 0, size);
            _disk.WriteFile(outName, comData);
            _terminal.WriteLine($"{outName}: {size} bytes (ORG {_minAddr:X4}h)");
        }
        else
        {
            _terminal.WriteLine("No code generated");
        }
    }

    private void WriteListing(string srcName, string[] lines)
    {
        var prnName = srcName.Replace(".ASM", ".PRN");
        var sb = new System.Text.StringBuilder();
        _pass = 2;
        _pc = _org;
        foreach (var line in lines)
        {
            ushort startPc = _pc;
            sb.AppendLine($"{startPc:X4}  {line}");
        }
        sb.AppendLine();
        sb.AppendLine("Symbol Table:");
        foreach (var kvp in _symbols.OrderBy(k => k.Key))
            sb.AppendLine($"  {kvp.Key,-16} {kvp.Value:X4}");
        _disk.WriteFile(prnName, sb.ToString());
    }

    private void AssembleLine(string rawLine)
    {
        // Strip comments
        string line = rawLine;
        int commentIdx = line.IndexOf(';');
        if (commentIdx >= 0) line = line[..commentIdx];
        line = line.TrimEnd();
        if (string.IsNullOrWhiteSpace(line)) return;

        // Parse label
        string? label = null;
        if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line[0] != ';')
        {
            int colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                label = line[..colonIdx].Trim();
                line = line[(colonIdx + 1)..].Trim();
            }
            else
            {
                // Check if first token is a label (followed by instruction on same line)
                var spaceIdx = line.IndexOfAny([' ', '\t']);
                if (spaceIdx > 0)
                {
                    var firstToken = line[..spaceIdx].TrimEnd();
                    var rest = line[spaceIdx..].Trim();
                    // If the first token isn't a mnemonic or directive, treat as label
                    if (!IsMnemonic(firstToken) && !IsDirective(firstToken))
                    {
                        label = firstToken;
                        line = rest;
                    }
                }
                else if (!IsMnemonic(line) && !IsDirective(line))
                {
                    label = line;
                    line = "";
                }
            }
        }
        else
        {
            line = line.Trim();
        }

        if (label != null && _pass == 1)
            _symbols[label] = _pc;

        if (string.IsNullOrWhiteSpace(line)) return;

        // Parse mnemonic and operands
        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        string mnemonic = parts[0].ToUpperInvariant();
        string operands = parts.Length > 1 ? parts[1].Trim() : "";

        ProcessInstruction(mnemonic, operands);
    }

    private void ProcessInstruction(string mnemonic, string operands)
    {
        switch (mnemonic)
        {
            // Directives
            case "ORG":
                _pc = _org = EvalExpr(operands);
                return;
            case "EQU":
                // Label already captured in pass 1; in pass 1 update value
                return;
            case "DB":
                EmitDbDirective(operands);
                return;
            case "DW":
                EmitDwDirective(operands);
                return;
            case "DS":
                _pc += EvalExpr(operands);
                return;
            case "END":
                return;

            // No-operand instructions
            case "NOP": Emit(0x00); return;
            case "HLT": Emit(0x76); return;
            case "RLC": Emit(0x07); return;
            case "RRC": Emit(0x0F); return;
            case "RAL": Emit(0x17); return;
            case "RAR": Emit(0x1F); return;
            case "DAA": Emit(0x27); return;
            case "CMA": Emit(0x2F); return;
            case "STC": Emit(0x37); return;
            case "CMC": Emit(0x3F); return;
            case "RET": Emit(0xC9); return;
            case "PCHL": Emit(0xE9); return;
            case "SPHL": Emit(0xF9); return;
            case "XTHL": Emit(0xE3); return;
            case "XCHG": Emit(0xEB); return;
            case "EI": Emit(0xFB); return;
            case "DI": Emit(0xF3); return;

            // Conditional returns
            case "RNZ": Emit(0xC0); return;
            case "RZ": Emit(0xC8); return;
            case "RNC": Emit(0xD0); return;
            case "RC": Emit(0xD8); return;
            case "RPO": Emit(0xE0); return;
            case "RPE": Emit(0xE8); return;
            case "RP": Emit(0xF0); return;
            case "RM": Emit(0xF8); return;

            // PUSH/POP
            case "PUSH":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0xC5); return;
                    case "D": Emit(0xD5); return;
                    case "H": Emit(0xE5); return;
                    case "PSW": Emit(0xF5); return;
                    default: Error("Invalid register pair"); return;
                }
            case "POP":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0xC1); return;
                    case "D": Emit(0xD1); return;
                    case "H": Emit(0xE1); return;
                    case "PSW": Emit(0xF1); return;
                    default: Error("Invalid register pair"); return;
                }

            // INX/DCX
            case "INX":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0x03); return;
                    case "D": Emit(0x13); return;
                    case "H": Emit(0x23); return;
                    case "SP": Emit(0x33); return;
                    default: Error("Invalid register pair"); return;
                }
            case "DCX":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0x0B); return;
                    case "D": Emit(0x1B); return;
                    case "H": Emit(0x2B); return;
                    case "SP": Emit(0x3B); return;
                    default: Error("Invalid register pair"); return;
                }

            // DAD
            case "DAD":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0x09); return;
                    case "D": Emit(0x19); return;
                    case "H": Emit(0x29); return;
                    case "SP": Emit(0x39); return;
                    default: Error("Invalid register pair"); return;
                }

            // LXI rp, d16
            case "LXI":
            {
                var (rp, imm) = SplitRegImm(operands);
                ushort val = EvalExpr(imm);
                switch (rp.ToUpperInvariant())
                {
                    case "B": Emit(0x01); EmitWord(val); return;
                    case "D": Emit(0x11); EmitWord(val); return;
                    case "H": Emit(0x21); EmitWord(val); return;
                    case "SP": Emit(0x31); EmitWord(val); return;
                    default: Error("Invalid register pair"); return;
                }
            }

            // MOV r,r
            case "MOV":
            {
                var (dst, src) = SplitTwoRegs(operands);
                int d = RegIndex(dst);
                int s = RegIndex(src);
                if (d < 0 || s < 0) { Error("Invalid register"); return; }
                Emit((byte)(0x40 | (d << 3) | s));
                return;
            }

            // MVI r, d8
            case "MVI":
            {
                var (reg, imm) = SplitRegImm(operands);
                int r = RegIndex(reg);
                if (r < 0) { Error("Invalid register"); return; }
                Emit((byte)(0x06 | (r << 3)));
                Emit((byte)EvalExpr(imm));
                return;
            }

            // INR/DCR
            case "INR":
            {
                int r = RegIndex(operands.Trim());
                if (r < 0) { Error("Invalid register"); return; }
                Emit((byte)(0x04 | (r << 3)));
                return;
            }
            case "DCR":
            {
                int r = RegIndex(operands.Trim());
                if (r < 0) { Error("Invalid register"); return; }
                Emit((byte)(0x05 | (r << 3)));
                return;
            }

            // Arithmetic with register
            case "ADD": EmitRegOp(0x80, operands); return;
            case "ADC": EmitRegOp(0x88, operands); return;
            case "SUB": EmitRegOp(0x90, operands); return;
            case "SBB": EmitRegOp(0x98, operands); return;
            case "ANA": EmitRegOp(0xA0, operands); return;
            case "XRA": EmitRegOp(0xA8, operands); return;
            case "ORA": EmitRegOp(0xB0, operands); return;
            case "CMP": EmitRegOp(0xB8, operands); return;

            // Immediate arithmetic
            case "ADI": Emit(0xC6); Emit((byte)EvalExpr(operands)); return;
            case "ACI": Emit(0xCE); Emit((byte)EvalExpr(operands)); return;
            case "SUI": Emit(0xD6); Emit((byte)EvalExpr(operands)); return;
            case "SBI": Emit(0xDE); Emit((byte)EvalExpr(operands)); return;
            case "ANI": Emit(0xE6); Emit((byte)EvalExpr(operands)); return;
            case "XRI": Emit(0xEE); Emit((byte)EvalExpr(operands)); return;
            case "ORI": Emit(0xF6); Emit((byte)EvalExpr(operands)); return;
            case "CPI": Emit(0xFE); Emit((byte)EvalExpr(operands)); return;

            // Jumps
            case "JMP": Emit(0xC3); EmitWord(EvalExpr(operands)); return;
            case "JNZ": Emit(0xC2); EmitWord(EvalExpr(operands)); return;
            case "JZ": Emit(0xCA); EmitWord(EvalExpr(operands)); return;
            case "JNC": Emit(0xD2); EmitWord(EvalExpr(operands)); return;
            case "JC": Emit(0xDA); EmitWord(EvalExpr(operands)); return;
            case "JPO": Emit(0xE2); EmitWord(EvalExpr(operands)); return;
            case "JPE": Emit(0xEA); EmitWord(EvalExpr(operands)); return;
            case "JP": Emit(0xF2); EmitWord(EvalExpr(operands)); return;
            case "JM": Emit(0xFA); EmitWord(EvalExpr(operands)); return;

            // Calls
            case "CALL": Emit(0xCD); EmitWord(EvalExpr(operands)); return;
            case "CNZ": Emit(0xC4); EmitWord(EvalExpr(operands)); return;
            case "CZ": Emit(0xCC); EmitWord(EvalExpr(operands)); return;
            case "CNC": Emit(0xD4); EmitWord(EvalExpr(operands)); return;
            case "CC": Emit(0xDC); EmitWord(EvalExpr(operands)); return;
            case "CPO": Emit(0xE4); EmitWord(EvalExpr(operands)); return;
            case "CPE": Emit(0xEC); EmitWord(EvalExpr(operands)); return;
            case "CP": Emit(0xF4); EmitWord(EvalExpr(operands)); return;
            case "CM": Emit(0xFC); EmitWord(EvalExpr(operands)); return;

            // RST
            case "RST":
            {
                int n = (int)EvalExpr(operands);
                if (n < 0 || n > 7) { Error("RST 0-7"); return; }
                Emit((byte)(0xC7 | (n << 3)));
                return;
            }

            // I/O
            case "IN": Emit(0xDB); Emit((byte)EvalExpr(operands)); return;
            case "OUT": Emit(0xD3); Emit((byte)EvalExpr(operands)); return;

            // Memory reference
            case "STAX":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0x02); return;
                    case "D": Emit(0x12); return;
                    default: Error("STAX B or D only"); return;
                }
            case "LDAX":
                switch (operands.ToUpperInvariant())
                {
                    case "B": Emit(0x0A); return;
                    case "D": Emit(0x1A); return;
                    default: Error("LDAX B or D only"); return;
                }
            case "STA": Emit(0x32); EmitWord(EvalExpr(operands)); return;
            case "LDA": Emit(0x3A); EmitWord(EvalExpr(operands)); return;
            case "SHLD": Emit(0x22); EmitWord(EvalExpr(operands)); return;
            case "LHLD": Emit(0x2A); EmitWord(EvalExpr(operands)); return;

            default:
                Error($"Unknown mnemonic: {mnemonic}");
                return;
        }
    }

    private void EmitRegOp(byte baseOpcode, string operands)
    {
        int r = RegIndex(operands.Trim());
        if (r < 0) { Error("Invalid register"); return; }
        Emit((byte)(baseOpcode | r));
    }

    private int RegIndex(string reg) => reg.ToUpperInvariant() switch
    {
        "B" => 0, "C" => 1, "D" => 2, "E" => 3,
        "H" => 4, "L" => 5, "M" => 6, "A" => 7,
        _ => -1
    };

    private static (string reg, string imm) SplitRegImm(string operands)
    {
        int comma = operands.IndexOf(',');
        if (comma < 0) return (operands.Trim(), "0");
        return (operands[..comma].Trim(), operands[(comma + 1)..].Trim());
    }

    private static (string dst, string src) SplitTwoRegs(string operands)
    {
        int comma = operands.IndexOf(',');
        if (comma < 0) return (operands.Trim(), "A");
        return (operands[..comma].Trim(), operands[(comma + 1)..].Trim());
    }

    private ushort EvalExpr(string expr)
    {
        expr = expr.Trim();
        if (string.IsNullOrEmpty(expr)) return 0;

        // Handle $ (current address)
        expr = expr.Replace("$", _pc.ToString());

        // Handle simple binary operations
        foreach (char op in new[] { '+', '-' })
        {
            // Find operator not inside parens and not at start (for negative numbers)
            int depth = 0;
            for (int i = expr.Length - 1; i > 0; i--)
            {
                if (expr[i] == ')') depth++;
                else if (expr[i] == '(') depth--;
                else if (depth == 0 && expr[i] == op)
                {
                    var left = EvalExpr(expr[..i]);
                    var right = EvalExpr(expr[(i + 1)..]);
                    return op == '+' ? (ushort)(left + right) : (ushort)(left - right);
                }
            }
        }

        // Handle parentheses
        if (expr.StartsWith('(') && expr.EndsWith(')'))
            return EvalExpr(expr[1..^1]);

        // Handle NOT
        if (expr.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase))
            return (ushort)(~EvalExpr(expr[4..]));

        // Handle HIGH/LOW
        if (expr.StartsWith("HIGH ", StringComparison.OrdinalIgnoreCase))
            return (ushort)(EvalExpr(expr[5..]) >> 8);
        if (expr.StartsWith("LOW ", StringComparison.OrdinalIgnoreCase))
            return (ushort)(EvalExpr(expr[4..]) & 0xFF);

        // Hex: 0FFh or 0FFH or 0xNN
        if (expr.EndsWith('H') || expr.EndsWith('h'))
        {
            if (ushort.TryParse(expr[..^1], System.Globalization.NumberStyles.HexNumber, null, out ushort hval))
                return hval;
        }
        if (expr.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            if (ushort.TryParse(expr[2..], System.Globalization.NumberStyles.HexNumber, null, out ushort hval))
                return hval;
        }

        // Binary: 10101010B
        if (expr.EndsWith('B') || expr.EndsWith('b'))
        {
            try
            {
                return (ushort)Convert.ToInt32(expr[..^1], 2);
            }
            catch { }
        }

        // Octal: 377O or 377Q
        if (expr.EndsWith('O') || expr.EndsWith('o') || expr.EndsWith('Q') || expr.EndsWith('q'))
        {
            try
            {
                return (ushort)Convert.ToInt32(expr[..^1], 8);
            }
            catch { }
        }

        // Character constant: 'A'
        if (expr.Length == 3 && expr[0] == '\'' && expr[2] == '\'')
            return expr[1];

        // Decimal
        if (ushort.TryParse(expr, out ushort dval))
            return dval;

        // Symbol lookup
        if (_symbols.TryGetValue(expr, out ushort sval))
            return sval;

        if (_pass == 2)
            Error($"Undefined symbol: {expr}");
        return 0;
    }

    private void Emit(byte b)
    {
        if (_pass == 2)
        {
            _output[_pc] = b;
            if (_pc < _minAddr) _minAddr = _pc;
            if (_pc > _maxAddr) _maxAddr = _pc;
        }
        _pc++;
    }

    private void EmitWord(ushort w)
    {
        Emit((byte)(w & 0xFF));
        Emit((byte)(w >> 8));
    }

    private void EmitDbDirective(string operands)
    {
        // DB can have comma-separated bytes and strings
        var items = SplitDbOperands(operands);
        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.StartsWith('\'') && trimmed.EndsWith('\'') && trimmed.Length >= 2)
            {
                // String literal
                var s = trimmed[1..^1];
                foreach (char c in s)
                    Emit((byte)c);
            }
            else if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
            {
                var s = trimmed[1..^1];
                foreach (char c in s)
                    Emit((byte)c);
            }
            else
            {
                Emit((byte)EvalExpr(trimmed));
            }
        }
    }

    private void EmitDwDirective(string operands)
    {
        foreach (var item in operands.Split(','))
            EmitWord(EvalExpr(item.Trim()));
    }

    private static List<string> SplitDbOperands(string operands)
    {
        var result = new List<string>();
        bool inString = false;
        char stringChar = ' ';
        int start = 0;

        for (int i = 0; i < operands.Length; i++)
        {
            char c = operands[i];
            if (inString)
            {
                if (c == stringChar)
                    inString = false;
            }
            else if (c == '\'' || c == '"')
            {
                inString = true;
                stringChar = c;
            }
            else if (c == ',')
            {
                result.Add(operands[start..i]);
                start = i + 1;
            }
        }
        if (start < operands.Length)
            result.Add(operands[start..]);
        return result;
    }

    private void Error(string message)
    {
        _errors++;
        if (_pass == 2)
            _terminal.WriteLine($"Error line {_lineNum}: {message}");
    }

    private static bool IsMnemonic(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "NOP" or "HLT" or "RLC" or "RRC" or "RAL" or "RAR" or "DAA" or "CMA" or "STC" or "CMC" or
            "RET" or "PCHL" or "SPHL" or "XTHL" or "XCHG" or "EI" or "DI" or
            "RNZ" or "RZ" or "RNC" or "RC" or "RPO" or "RPE" or "RP" or "RM" or
            "PUSH" or "POP" or "INX" or "DCX" or "DAD" or "LXI" or "MOV" or "MVI" or
            "INR" or "DCR" or "ADD" or "ADC" or "SUB" or "SBB" or "ANA" or "XRA" or "ORA" or "CMP" or
            "ADI" or "ACI" or "SUI" or "SBI" or "ANI" or "XRI" or "ORI" or "CPI" or
            "JMP" or "JNZ" or "JZ" or "JNC" or "JC" or "JPO" or "JPE" or "JP" or "JM" or
            "CALL" or "CNZ" or "CZ" or "CNC" or "CC" or "CPO" or "CPE" or "CP" or "CM" or
            "RST" or "IN" or "OUT" or "STAX" or "LDAX" or "STA" or "LDA" or "SHLD" or "LHLD"
            => true,
            _ => false
        };
    }

    private static bool IsDirective(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "ORG" or "EQU" or "DB" or "DW" or "DS" or "END" => true,
            _ => false
        };
    }
}
