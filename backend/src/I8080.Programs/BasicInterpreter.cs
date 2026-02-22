using I8080.CpmSystem;

namespace I8080.Programs;

/// <summary>
/// MBASIC - A BASIC interpreter for CP/M.
/// Supports: PRINT, INPUT, LET, IF/THEN/ELSE, GOTO, GOSUB/RETURN,
/// FOR/NEXT, DIM, REM, DATA/READ/RESTORE, END, STOP, RUN, LIST,
/// NEW, LOAD, SAVE, PEEK, POKE, RND, ABS, INT, LEN, LEFT$, RIGHT$, MID$,
/// CHR$, ASC, STR$, VAL, TAB, SPC
/// </summary>
public sealed class BasicInterpreter
{
    private readonly ITerminal _terminal;
    private readonly VirtualDisk _disk;

    // Program storage: line number -> source text
    private readonly SortedDictionary<int, string> _program = new();

    // Variables
    private readonly Dictionary<string, Value> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Value[]> _arrays = new(StringComparer.OrdinalIgnoreCase);

    // Execution state
    private readonly Stack<ForState> _forStack = new();
    private readonly Stack<int> _gosubStack = new();
    private readonly List<int> _lineNumbers = [];
    private int _currentLineIdx;
    private bool _running;
    private readonly Random _rng = new();

    // DATA/READ
    private readonly List<string> _dataValues = [];
    private int _dataPointer;

    public BasicInterpreter(ITerminal terminal, VirtualDisk disk)
    {
        _terminal = terminal;
        _disk = disk;
    }

    public void Run(string args)
    {
        _terminal.WriteLine("MBASIC Ver 5.0");
        _terminal.WriteLine("CP/M BASIC Interpreter");
        _terminal.WriteLine("Ready");

        if (!string.IsNullOrWhiteSpace(args))
        {
            var fileName = args.Trim().ToUpperInvariant();
            if (!fileName.Contains('.')) fileName += ".BAS";
            LoadProgram(fileName);
        }

        // Interactive mode
        while (true)
        {
            _terminal.Write("] ");
            string line = _terminal.ReadLine().Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("BYE", StringComparison.OrdinalIgnoreCase))
                return;

            // Check if line starts with a number (program entry)
            int spaceIdx = line.IndexOf(' ');
            if (spaceIdx > 0 && int.TryParse(line[..spaceIdx], out int lineNum))
            {
                string rest = line[spaceIdx..].TrimStart();
                if (string.IsNullOrEmpty(rest))
                    _program.Remove(lineNum);
                else
                    _program[lineNum] = rest;
            }
            else if (int.TryParse(line, out int delLine))
            {
                _program.Remove(delLine);
            }
            else
            {
                // Direct execution
                try
                {
                    ExecuteLine(line);
                }
                catch (BasicException ex)
                {
                    _terminal.WriteLine($"?{ex.Message}");
                }
            }
        }
    }

    private void ExecuteProgram()
    {
        _running = true;
        _lineNumbers.Clear();
        _lineNumbers.AddRange(_program.Keys);
        _currentLineIdx = 0;
        _forStack.Clear();
        _gosubStack.Clear();
        CollectData();

        while (_running && _currentLineIdx < _lineNumbers.Count)
        {
            int lineNum = _lineNumbers[_currentLineIdx];
            string src = _program[lineNum];
            try
            {
                ExecuteLine(src);
            }
            catch (BasicException ex)
            {
                _terminal.WriteLine($"?{ex.Message} in line {lineNum}");
                _running = false;
                return;
            }
            if (_running)
                _currentLineIdx++;
        }
        _running = false;
    }

    private void CollectData()
    {
        _dataValues.Clear();
        _dataPointer = 0;
        foreach (var kvp in _program)
        {
            var line = kvp.Value.Trim();
            if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                var items = line[4..].Split(',');
                foreach (var item in items)
                    _dataValues.Add(item.Trim().Trim('"'));
            }
        }
    }

    private void ExecuteLine(string line)
    {
        // Handle multiple statements separated by ':'
        var statements = SplitStatements(line);
        foreach (var stmt in statements)
        {
            ExecuteStatement(stmt.Trim());
            if (!_running && _currentLineIdx >= 0) break;
        }
    }

    private static List<string> SplitStatements(string line)
    {
        var result = new List<string>();
        int depth = 0;
        bool inString = false;
        int start = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') inString = !inString;
            if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ':' && depth == 0)
                {
                    result.Add(line[start..i]);
                    start = i + 1;
                }
            }
        }
        result.Add(line[start..]);
        return result;
    }

    private void ExecuteStatement(string stmt)
    {
        if (string.IsNullOrWhiteSpace(stmt)) return;

        string keyword = GetKeyword(stmt, out string rest);

        switch (keyword)
        {
            case "REM": return;
            case "PRINT": case "?": DoPrint(rest); return;
            case "INPUT": DoInput(rest); return;
            case "LET": DoLet(rest); return;
            case "IF": DoIf(rest); return;
            case "GOTO": DoGoto(rest); return;
            case "GOSUB": DoGosub(rest); return;
            case "RETURN": DoReturn(); return;
            case "FOR": DoFor(rest); return;
            case "NEXT": DoNext(rest); return;
            case "DIM": DoDim(rest); return;
            case "END": case "STOP": _running = false; return;
            case "RUN": _variables.Clear(); _arrays.Clear(); ExecuteProgram(); return;
            case "LIST": DoList(rest); return;
            case "NEW": _program.Clear(); _variables.Clear(); _arrays.Clear(); return;
            case "LOAD": DoLoad(rest); return;
            case "SAVE": DoSave(rest); return;
            case "DATA": return; // Handled in CollectData
            case "READ": DoRead(rest); return;
            case "RESTORE": _dataPointer = 0; return;
            case "POKE": DoPoke(rest); return;
            case "CLS": _terminal.Write("\x1B[2J\x1B[H"); return;
            case "FILES": DoFiles(); return;
            default:
                // Try as LET assignment (variable = expr)
                if (rest.Length > 0 || stmt.Contains('='))
                {
                    DoLet(stmt);
                }
                else
                {
                    throw new BasicException("SYNTAX ERROR");
                }
                return;
        }
    }

    private static string GetKeyword(string stmt, out string rest)
    {
        stmt = stmt.TrimStart();
        if (stmt.StartsWith("?"))
        {
            rest = stmt[1..].TrimStart();
            return "?";
        }

        int i = 0;
        while (i < stmt.Length && char.IsLetter(stmt[i])) i++;
        if (i == 0) { rest = stmt; return ""; }

        string kw = stmt[..i].ToUpperInvariant();
        rest = stmt[i..].TrimStart();

        // Check if this is actually a keyword
        if (IsKeyword(kw))
            return kw;

        // Not a keyword, return empty and let the caller handle it
        rest = stmt;
        return "";
    }

    private static bool IsKeyword(string kw) => kw switch
    {
        "REM" or "PRINT" or "INPUT" or "LET" or "IF" or "THEN" or "ELSE" or "GOTO" or
        "GOSUB" or "RETURN" or "FOR" or "TO" or "STEP" or "NEXT" or "DIM" or "END" or
        "STOP" or "RUN" or "LIST" or "NEW" or "LOAD" or "SAVE" or "DATA" or "READ" or
        "RESTORE" or "POKE" or "CLS" or "FILES" => true,
        _ => false
    };

    #region Statement Implementations

    private void DoPrint(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            _terminal.WriteLine();
            return;
        }

        var parts = SplitPrintArgs(args);
        bool newline = true;
        foreach (var part in parts)
        {
            if (part == ";") { newline = false; continue; }
            if (part == ",") { _terminal.Write("\t"); newline = false; continue; }
            newline = true;
            var val = EvalExpression(part.Trim());
            _terminal.Write(val.AsString());
        }
        if (newline)
            _terminal.WriteLine();
    }

    private static List<string> SplitPrintArgs(string args)
    {
        var result = new List<string>();
        int depth = 0;
        bool inString = false;
        int start = 0;
        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"') inString = !inString;
            if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0 && (c == ';' || c == ','))
                {
                    if (i > start)
                        result.Add(args[start..i]);
                    result.Add(c.ToString());
                    start = i + 1;
                }
            }
        }
        if (start < args.Length)
            result.Add(args[start..]);
        return result;
    }

    private void DoInput(string args)
    {
        string prompt = "? ";
        string varName = args;

        // Check for prompt string
        int semiIdx = args.IndexOf(';');
        if (semiIdx > 0 && args[0] == '"')
        {
            int endQuote = args.IndexOf('"', 1);
            if (endQuote > 0)
            {
                prompt = args[1..endQuote];
                varName = args[(semiIdx + 1)..].Trim();
            }
        }

        var varNames = varName.Split(',').Select(v => v.Trim()).ToArray();
        _terminal.Write(prompt);
        string input = _terminal.ReadLine();
        var values = input.Split(',');

        for (int i = 0; i < varNames.Length; i++)
        {
            string val = i < values.Length ? values[i].Trim() : "0";
            if (varNames[i].EndsWith('$'))
                SetVariable(varNames[i], new Value(val));
            else if (double.TryParse(val, out double d))
                SetVariable(varNames[i], new Value(d));
            else
                SetVariable(varNames[i], new Value(val));
        }
    }

    private void DoLet(string args)
    {
        int eqIdx = args.IndexOf('=');
        if (eqIdx < 0) throw new BasicException("SYNTAX ERROR");

        string varExpr = args[..eqIdx].Trim();
        string valExpr = args[(eqIdx + 1)..].Trim();
        var val = EvalExpression(valExpr);

        // Check for array element
        int parenIdx = varExpr.IndexOf('(');
        if (parenIdx > 0)
        {
            string arrName = varExpr[..parenIdx].Trim().ToUpperInvariant();
            string idxExpr = varExpr[(parenIdx + 1)..varExpr.LastIndexOf(')')];
            int idx = (int)EvalExpression(idxExpr).AsNumber();
            if (_arrays.TryGetValue(arrName, out var arr) && idx >= 0 && idx < arr.Length)
                arr[idx] = val;
            else
                throw new BasicException("SUBSCRIPT OUT OF RANGE");
            return;
        }

        SetVariable(varExpr.Trim(), val);
    }

    private void DoIf(string args)
    {
        // Find THEN
        int thenIdx = args.IndexOf("THEN", StringComparison.OrdinalIgnoreCase);
        if (thenIdx < 0) throw new BasicException("SYNTAX ERROR");

        string condition = args[..thenIdx].Trim();
        string thenPart = args[(thenIdx + 4)..].Trim();

        // Check for ELSE
        string? elsePart = null;
        int elseIdx = FindElse(thenPart);
        if (elseIdx >= 0)
        {
            elsePart = thenPart[(elseIdx + 4)..].Trim();
            thenPart = thenPart[..elseIdx].Trim();
        }

        var result = EvalExpression(condition);
        if (result.AsNumber() != 0)
        {
            // THEN: could be a line number or statement
            if (int.TryParse(thenPart, out int lineNum))
                DoGoto(lineNum.ToString());
            else
                ExecuteLine(thenPart);
        }
        else if (elsePart != null)
        {
            if (int.TryParse(elsePart, out int lineNum))
                DoGoto(lineNum.ToString());
            else
                ExecuteLine(elsePart);
        }
    }

    private static int FindElse(string s)
    {
        int depth = 0;
        bool inString = false;
        for (int i = 0; i < s.Length - 3; i++)
        {
            if (s[i] == '"') inString = !inString;
            if (!inString)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') depth--;
                else if (depth == 0 && s[i..].StartsWith("ELSE", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 4 >= s.Length || !char.IsLetter(s[i + 4]))
                        return i;
                }
            }
        }
        return -1;
    }

    private void DoGoto(string args)
    {
        int lineNum = (int)EvalExpression(args.Trim()).AsNumber();
        int idx = _lineNumbers.IndexOf(lineNum);
        if (idx < 0) throw new BasicException("UNDEFINED LINE " + lineNum);
        _currentLineIdx = idx - 1; // -1 because main loop will increment
    }

    private void DoGosub(string args)
    {
        int lineNum = (int)EvalExpression(args.Trim()).AsNumber();
        int idx = _lineNumbers.IndexOf(lineNum);
        if (idx < 0) throw new BasicException("UNDEFINED LINE " + lineNum);
        _gosubStack.Push(_currentLineIdx);
        _currentLineIdx = idx - 1;
    }

    private void DoReturn()
    {
        if (_gosubStack.Count == 0) throw new BasicException("RETURN WITHOUT GOSUB");
        _currentLineIdx = _gosubStack.Pop();
    }

    private void DoFor(string args)
    {
        int eqIdx = args.IndexOf('=');
        int toIdx = args.IndexOf("TO", eqIdx + 1, StringComparison.OrdinalIgnoreCase);
        if (eqIdx < 0 || toIdx < 0) throw new BasicException("SYNTAX ERROR");

        string varName = args[..eqIdx].Trim();
        string startExpr = args[(eqIdx + 1)..toIdx].Trim();
        string rest = args[(toIdx + 2)..].Trim();

        double startVal = EvalExpression(startExpr).AsNumber();
        double stepVal = 1;
        double endVal;

        int stepIdx = rest.IndexOf("STEP", StringComparison.OrdinalIgnoreCase);
        if (stepIdx >= 0)
        {
            endVal = EvalExpression(rest[..stepIdx].Trim()).AsNumber();
            stepVal = EvalExpression(rest[(stepIdx + 4)..].Trim()).AsNumber();
        }
        else
        {
            endVal = EvalExpression(rest).AsNumber();
        }

        SetVariable(varName, new Value(startVal));
        _forStack.Push(new ForState
        {
            VarName = varName,
            EndValue = endVal,
            StepValue = stepVal,
            LineIndex = _currentLineIdx
        });
    }

    private void DoNext(string args)
    {
        if (_forStack.Count == 0) throw new BasicException("NEXT WITHOUT FOR");

        string varName = args.Trim();
        var forState = _forStack.Peek();

        if (!string.IsNullOrEmpty(varName) &&
            !varName.Equals(forState.VarName, StringComparison.OrdinalIgnoreCase))
            throw new BasicException("NEXT WITHOUT FOR");

        double current = GetVariable(forState.VarName).AsNumber() + forState.StepValue;
        SetVariable(forState.VarName, new Value(current));

        bool done = forState.StepValue > 0
            ? current > forState.EndValue
            : current < forState.EndValue;

        if (done)
        {
            _forStack.Pop();
        }
        else
        {
            _currentLineIdx = forState.LineIndex;
        }
    }

    private void DoDim(string args)
    {
        foreach (var decl in args.Split(','))
        {
            var trimmed = decl.Trim();
            int paren = trimmed.IndexOf('(');
            if (paren < 0) continue;
            string name = trimmed[..paren].Trim().ToUpperInvariant();
            string sizeExpr = trimmed[(paren + 1)..trimmed.LastIndexOf(')')];
            int size = (int)EvalExpression(sizeExpr).AsNumber() + 1;
            _arrays[name] = new Value[size];
            for (int i = 0; i < size; i++)
                _arrays[name][i] = new Value(0.0);
        }
    }

    private void DoRead(string args)
    {
        foreach (var varName in args.Split(','))
        {
            string name = varName.Trim();
            if (_dataPointer >= _dataValues.Count)
                throw new BasicException("OUT OF DATA");
            string val = _dataValues[_dataPointer++];
            if (name.EndsWith('$'))
                SetVariable(name, new Value(val));
            else if (double.TryParse(val, out double d))
                SetVariable(name, new Value(d));
            else
                SetVariable(name, new Value(val));
        }
    }

    private void DoPoke(string args)
    {
        var parts = args.Split(',');
        if (parts.Length != 2) throw new BasicException("SYNTAX ERROR");
        // POKE is a no-op in our system (no raw memory access from BASIC)
        _terminal.WriteLine("(POKE ignored - no raw memory access)");
    }

    private void DoList(string args)
    {
        int start = 0, end = int.MaxValue;
        if (!string.IsNullOrEmpty(args))
        {
            var parts = args.Split('-');
            if (parts.Length == 2)
            {
                if (!string.IsNullOrEmpty(parts[0])) start = int.Parse(parts[0]);
                if (!string.IsNullOrEmpty(parts[1])) end = int.Parse(parts[1]);
            }
            else
            {
                start = end = int.Parse(args);
            }
        }

        foreach (var kvp in _program)
        {
            if (kvp.Key >= start && kvp.Key <= end)
                _terminal.WriteLine($"{kvp.Key} {kvp.Value}");
        }
    }

    private void DoLoad(string args)
    {
        var fileName = args.Trim().Trim('"').ToUpperInvariant();
        if (!fileName.Contains('.')) fileName += ".BAS";
        LoadProgram(fileName);
    }

    private void DoSave(string args)
    {
        var fileName = args.Trim().Trim('"').ToUpperInvariant();
        if (!fileName.Contains('.')) fileName += ".BAS";
        var sb = new System.Text.StringBuilder();
        foreach (var kvp in _program)
            sb.AppendLine($"{kvp.Key} {kvp.Value}");
        _disk.WriteFile(fileName, sb.ToString());
        _terminal.WriteLine($"Saved {fileName}");
    }

    private void DoFiles()
    {
        foreach (var file in _disk.GetFiles("*.BAS").OrderBy(f => f))
            _terminal.Write($"{file}  ");
        _terminal.WriteLine();
    }

    private void LoadProgram(string fileName)
    {
        var text = _disk.ReadFileAsText(fileName);
        if (text == null)
        {
            _terminal.WriteLine($"File not found: {fileName}");
            return;
        }
        _program.Clear();
        foreach (var line in text.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int spaceIdx = line.IndexOf(' ');
            if (spaceIdx > 0 && int.TryParse(line[..spaceIdx], out int num))
                _program[num] = line[spaceIdx..].TrimStart();
        }
        _terminal.WriteLine($"Loaded {fileName}, {_program.Count} lines");
    }

    #endregion

    #region Expression Evaluator

    private Value EvalExpression(string expr)
    {
        expr = expr.Trim();
        if (string.IsNullOrEmpty(expr)) return new Value(0.0);

        var tokens = Tokenize(expr);
        int pos = 0;
        return ParseOr(tokens, ref pos);
    }

    private Value ParseOr(List<Token> tokens, ref int pos)
    {
        var left = ParseAnd(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Text.Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseAnd(tokens, ref pos);
            left = new Value((left.AsNumber() != 0 || right.AsNumber() != 0) ? -1.0 : 0.0);
        }
        return left;
    }

    private Value ParseAnd(List<Token> tokens, ref int pos)
    {
        var left = ParseNot(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Text.Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseNot(tokens, ref pos);
            left = new Value((left.AsNumber() != 0 && right.AsNumber() != 0) ? -1.0 : 0.0);
        }
        return left;
    }

    private Value ParseNot(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Text.Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var val = ParseComparison(tokens, ref pos);
            return new Value(val.AsNumber() == 0 ? -1.0 : 0.0);
        }
        return ParseComparison(tokens, ref pos);
    }

    private Value ParseComparison(List<Token> tokens, ref int pos)
    {
        var left = ParseAddSub(tokens, ref pos);
        while (pos < tokens.Count)
        {
            string op = tokens[pos].Text;
            if (op is "=" or "<" or ">" or "<=" or ">=" or "<>" or "><")
            {
                pos++;
                var right = ParseAddSub(tokens, ref pos);
                bool result = op switch
                {
                    "=" => left.IsString ? left.AsString() == right.AsString() : left.AsNumber() == right.AsNumber(),
                    "<" => left.AsNumber() < right.AsNumber(),
                    ">" => left.AsNumber() > right.AsNumber(),
                    "<=" => left.AsNumber() <= right.AsNumber(),
                    ">=" => left.AsNumber() >= right.AsNumber(),
                    "<>" or "><" => left.IsString ? left.AsString() != right.AsString() : left.AsNumber() != right.AsNumber(),
                    _ => false
                };
                left = new Value(result ? -1.0 : 0.0);
            }
            else break;
        }
        return left;
    }

    private Value ParseAddSub(List<Token> tokens, ref int pos)
    {
        var left = ParseMulDiv(tokens, ref pos);
        while (pos < tokens.Count)
        {
            string op = tokens[pos].Text;
            if (op == "+" || op == "-")
            {
                pos++;
                var right = ParseMulDiv(tokens, ref pos);
                if (op == "+" && (left.IsString || right.IsString))
                    left = new Value(left.AsString() + right.AsString());
                else if (op == "+")
                    left = new Value(left.AsNumber() + right.AsNumber());
                else
                    left = new Value(left.AsNumber() - right.AsNumber());
            }
            else break;
        }
        return left;
    }

    private Value ParseMulDiv(List<Token> tokens, ref int pos)
    {
        var left = ParsePower(tokens, ref pos);
        while (pos < tokens.Count)
        {
            string op = tokens[pos].Text;
            if (op is "*" or "/" or "MOD")
            {
                pos++;
                var right = ParsePower(tokens, ref pos);
                left = op switch
                {
                    "*" => new Value(left.AsNumber() * right.AsNumber()),
                    "/" => right.AsNumber() == 0 ? throw new BasicException("DIVISION BY ZERO") : new Value(left.AsNumber() / right.AsNumber()),
                    _ => new Value((double)((int)left.AsNumber() % (int)right.AsNumber()))
                };
            }
            else break;
        }
        return left;
    }

    private Value ParsePower(List<Token> tokens, ref int pos)
    {
        var left = ParseUnary(tokens, ref pos);
        if (pos < tokens.Count && tokens[pos].Text == "^")
        {
            pos++;
            var right = ParseUnary(tokens, ref pos);
            left = new Value(Math.Pow(left.AsNumber(), right.AsNumber()));
        }
        return left;
    }

    private Value ParseUnary(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Text == "-")
        {
            pos++;
            return new Value(-ParsePrimary(tokens, ref pos).AsNumber());
        }
        if (pos < tokens.Count && tokens[pos].Text == "+")
        {
            pos++;
        }
        return ParsePrimary(tokens, ref pos);
    }

    private Value ParsePrimary(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count) return new Value(0.0);

        var token = tokens[pos];

        // Parenthesized expression
        if (token.Text == "(")
        {
            pos++;
            var val = ParseOr(tokens, ref pos);
            if (pos < tokens.Count && tokens[pos].Text == ")") pos++;
            return val;
        }

        // String literal
        if (token.Type == TokenType.String)
        {
            pos++;
            return new Value(token.Text);
        }

        // Number
        if (token.Type == TokenType.Number)
        {
            pos++;
            return new Value(double.Parse(token.Text));
        }

        // Built-in functions
        string upper = token.Text.ToUpperInvariant();
        if (IsFunction(upper))
        {
            pos++;
            return EvalFunction(upper, tokens, ref pos);
        }

        // Variable or array element
        if (token.Type == TokenType.Identifier)
        {
            pos++;
            string varName = token.Text;

            // Check for array access
            if (pos < tokens.Count && tokens[pos].Text == "(")
            {
                pos++; // skip (
                int idx = (int)ParseOr(tokens, ref pos).AsNumber();
                if (pos < tokens.Count && tokens[pos].Text == ")") pos++;
                string arrName = varName.ToUpperInvariant();
                if (_arrays.TryGetValue(arrName, out var arr))
                {
                    if (idx >= 0 && idx < arr.Length) return arr[idx];
                    throw new BasicException("SUBSCRIPT OUT OF RANGE");
                }
                // Auto-dim array of size 10
                _arrays[arrName] = new Value[11];
                for (int i = 0; i < 11; i++) _arrays[arrName][i] = new Value(0.0);
                if (idx >= 0 && idx < 11) return _arrays[arrName][idx];
                throw new BasicException("SUBSCRIPT OUT OF RANGE");
            }

            return GetVariable(varName);
        }

        pos++;
        return new Value(0.0);
    }

    private static bool IsFunction(string name) => name switch
    {
        "ABS" or "INT" or "SQR" or "SIN" or "COS" or "TAN" or "ATN" or "EXP" or "LOG" or
        "RND" or "SGN" or "PEEK" or "LEN" or "ASC" or "VAL" or "CHR$" or "STR$" or
        "LEFT$" or "RIGHT$" or "MID$" or "TAB" or "SPC" or "FRE" => true,
        _ => false
    };

    private Value EvalFunction(string name, List<Token> tokens, ref int pos)
    {
        // Expect (
        if (pos >= tokens.Count || tokens[pos].Text != "(")
        {
            // Some functions work without parens
            if (name == "RND") return new Value(_rng.NextDouble());
            throw new BasicException("SYNTAX ERROR");
        }
        pos++; // skip (

        var args = new List<Value>();
        if (pos < tokens.Count && tokens[pos].Text != ")")
        {
            args.Add(ParseOr(tokens, ref pos));
            while (pos < tokens.Count && tokens[pos].Text == ",")
            {
                pos++;
                args.Add(ParseOr(tokens, ref pos));
            }
        }
        if (pos < tokens.Count && tokens[pos].Text == ")") pos++;

        return name switch
        {
            "ABS" => new Value(Math.Abs(args[0].AsNumber())),
            "INT" => new Value(Math.Floor(args[0].AsNumber())),
            "SQR" => new Value(Math.Sqrt(args[0].AsNumber())),
            "SIN" => new Value(Math.Sin(args[0].AsNumber())),
            "COS" => new Value(Math.Cos(args[0].AsNumber())),
            "TAN" => new Value(Math.Tan(args[0].AsNumber())),
            "ATN" => new Value(Math.Atan(args[0].AsNumber())),
            "EXP" => new Value(Math.Exp(args[0].AsNumber())),
            "LOG" => new Value(Math.Log(args[0].AsNumber())),
            "RND" => new Value(_rng.NextDouble() * (args.Count > 0 ? args[0].AsNumber() : 1)),
            "SGN" => new Value(Math.Sign(args[0].AsNumber())),
            "PEEK" => new Value(0.0), // No raw memory access
            "FRE" => new Value(32768.0), // Fake free memory
            "LEN" => new Value(args[0].AsString().Length),
            "ASC" => new Value(args[0].AsString().Length > 0 ? args[0].AsString()[0] : 0.0),
            "VAL" => double.TryParse(args[0].AsString(), out double v) ? new Value(v) : new Value(0.0),
            "CHR$" => new Value(((char)(int)args[0].AsNumber()).ToString()),
            "STR$" => new Value(args[0].AsNumber().ToString()),
            "LEFT$" => new Value(args[0].AsString()[..Math.Min((int)args[1].AsNumber(), args[0].AsString().Length)]),
            "RIGHT$" => new Value(args[0].AsString().Length >= (int)args[1].AsNumber()
                ? args[0].AsString()[^(int)args[1].AsNumber()..] : args[0].AsString()),
            "MID$" => EvalMid(args),
            "TAB" => new Value(new string(' ', Math.Max(0, (int)args[0].AsNumber()))),
            "SPC" => new Value(new string(' ', Math.Max(0, (int)args[0].AsNumber()))),
            _ => throw new BasicException($"UNDEFINED FUNCTION {name}")
        };
    }

    private static Value EvalMid(List<Value> args)
    {
        string s = args[0].AsString();
        int start = Math.Max(1, (int)args[1].AsNumber()) - 1; // BASIC is 1-based
        if (start >= s.Length) return new Value("");
        int len = args.Count > 2 ? (int)args[2].AsNumber() : s.Length - start;
        len = Math.Min(len, s.Length - start);
        return new Value(s.Substring(start, len));
    }

    #endregion

    #region Tokenizer

    private static List<Token> Tokenize(string expr)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // String literal
            if (c == '"')
            {
                int end = expr.IndexOf('"', i + 1);
                if (end < 0) end = expr.Length;
                tokens.Add(new Token(TokenType.String, expr[(i + 1)..end]));
                i = end + 1;
                continue;
            }

            // Number
            if (char.IsDigit(c) || (c == '.' && i + 1 < expr.Length && char.IsDigit(expr[i + 1])))
            {
                int start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                if (i < expr.Length && (expr[i] == 'E' || expr[i] == 'e'))
                {
                    i++;
                    if (i < expr.Length && (expr[i] == '+' || expr[i] == '-')) i++;
                    while (i < expr.Length && char.IsDigit(expr[i])) i++;
                }
                tokens.Add(new Token(TokenType.Number, expr[start..i]));
                continue;
            }

            // Two-char operators
            if (i + 1 < expr.Length)
            {
                string two = expr.Substring(i, 2);
                if (two is "<=" or ">=" or "<>" or "><")
                {
                    tokens.Add(new Token(TokenType.Operator, two));
                    i += 2;
                    continue;
                }
            }

            // Single-char operators
            if ("+-*/^()=<>,;".Contains(c))
            {
                tokens.Add(new Token(TokenType.Operator, c.ToString()));
                i++;
                continue;
            }

            // Identifier or keyword
            if (char.IsLetter(c))
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '$' || expr[i] == '%')) i++;
                tokens.Add(new Token(TokenType.Identifier, expr[start..i]));
                continue;
            }

            i++; // Skip unknown
        }
        return tokens;
    }

    private enum TokenType { Number, String, Identifier, Operator }
    private record Token(TokenType Type, string Text);

    #endregion

    #region Variable Access

    private Value GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var val)) return val;
        // Default: 0 for numeric, "" for string
        return name.EndsWith('$') ? new Value("") : new Value(0.0);
    }

    private void SetVariable(string name, Value value)
    {
        _variables[name] = value;
    }

    #endregion

    #region Value Type

    public readonly struct Value
    {
        private readonly double _number;
        private readonly string? _string;
        public bool IsString => _string != null;

        public Value(double number) { _number = number; _string = null; }
        public Value(string str) { _number = 0; _string = str; }

        public double AsNumber() => _string != null
            ? (double.TryParse(_string, out double d) ? d : 0)
            : _number;

        public string AsString() => _string ?? FormatNumber(_number);

        private static string FormatNumber(double d)
        {
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
                return ((long)d).ToString();
            return d.ToString("G");
        }
    }

    #endregion

    private class ForState
    {
        public required string VarName { get; set; }
        public double EndValue { get; set; }
        public double StepValue { get; set; }
        public int LineIndex { get; set; }
    }

    private class BasicException(string message) : Exception(message);
}
