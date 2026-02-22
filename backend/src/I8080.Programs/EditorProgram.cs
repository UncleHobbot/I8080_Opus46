using I8080.CpmSystem;

namespace I8080.Programs;

/// <summary>
/// ED - CP/M-style line editor.
/// Commands: I(nsert), D(elete), L(ist), T(ype), S(earch), R(eplace),
///           F(ind), N(umber), P(age), W(rite), E(xit), Q(uit), H(elp)
/// </summary>
public sealed class EditorProgram
{
    private readonly ITerminal _terminal;
    private readonly VirtualDisk _disk;
    private List<string> _lines = [];
    private int _currentLine; // 0-based
    private string _fileName = "";
    private bool _modified;

    public EditorProgram(ITerminal terminal, VirtualDisk disk)
    {
        _terminal = terminal;
        _disk = disk;
    }

    public void Run(string args)
    {
        _fileName = args.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(_fileName))
        {
            _terminal.WriteLine("ED - Text Editor");
            _terminal.WriteLine("Usage: ED filename");
            return;
        }

        if (!_fileName.Contains('.')) _fileName += ".TXT";

        // Load existing file
        var text = _disk.ReadFileAsText(_fileName);
        if (text != null)
        {
            _lines = [.. text.Split('\n').Select(l => l.TrimEnd('\r'))];
            _terminal.WriteLine($" : {_lines.Count} lines read");
        }
        else
        {
            _lines = [];
            _terminal.WriteLine(" : New file");
        }
        _currentLine = 0;
        _modified = false;

        // Command loop
        while (true)
        {
            _terminal.Write("*");
            string cmd = _terminal.ReadLine().Trim();
            if (string.IsNullOrEmpty(cmd)) continue;

            char op = char.ToUpper(cmd[0]);
            string param = cmd.Length > 1 ? cmd[1..].Trim() : "";

            switch (op)
            {
                case 'I': Insert(param); break;
                case 'D': Delete(param); break;
                case 'L': ListLines(param); break;
                case 'T': TypeLine(param); break;
                case 'S': Search(param); break;
                case 'R': Replace(param); break;
                case 'N': NumberedList(param); break;
                case 'P': Page(param); break;
                case 'W': WriteFile(); break;
                case 'E': if (ExitSave()) return; break;
                case 'Q': if (Quit()) return; break;
                case 'H': Help(); break;
                case 'G': Goto(param); break;
                case 'K': Kill(param); break;
                case 'A': Append(param); break;

                // Numeric: go to line
                default:
                    if (int.TryParse(cmd, out int lineNum))
                    {
                        lineNum--;
                        if (lineNum >= 0 && lineNum < _lines.Count)
                        {
                            _currentLine = lineNum;
                            _terminal.WriteLine($"{_currentLine + 1}: {_lines[_currentLine]}");
                        }
                        else
                            _terminal.WriteLine("?");
                    }
                    else
                    {
                        _terminal.WriteLine("?");
                    }
                    break;
            }
        }
    }

    private void Insert(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _lines.Insert(_currentLine, text);
            _currentLine++;
            _modified = true;
            return;
        }

        _terminal.WriteLine("Insert mode (enter '.' on a line by itself to exit):");
        while (true)
        {
            _terminal.Write($"{_currentLine + 1}: ");
            string line = _terminal.ReadLine();
            if (line == ".") break;
            _lines.Insert(_currentLine, line);
            _currentLine++;
            _modified = true;
        }
    }

    private void Delete(string param)
    {
        if (_lines.Count == 0) { _terminal.WriteLine("?"); return; }

        int count = 1;
        if (!string.IsNullOrEmpty(param) && int.TryParse(param, out int n))
            count = n;

        for (int i = 0; i < count && _currentLine < _lines.Count; i++)
        {
            _terminal.WriteLine($"  {_lines[_currentLine]}");
            _lines.RemoveAt(_currentLine);
            _modified = true;
        }
        if (_currentLine >= _lines.Count && _lines.Count > 0)
            _currentLine = _lines.Count - 1;
    }

    private void ListLines(string param)
    {
        if (_lines.Count == 0) { _terminal.WriteLine("(empty)"); return; }

        int start = _currentLine;
        int count = 23;
        if (!string.IsNullOrEmpty(param))
        {
            var parts = param.Split(',');
            if (parts.Length == 2)
            {
                int.TryParse(parts[0], out start);
                int.TryParse(parts[1], out int end);
                start--; count = end - start;
            }
            else if (int.TryParse(param, out int c))
            {
                count = c;
            }
        }

        start = Math.Max(0, start);
        int end2 = Math.Min(_lines.Count, start + count);
        for (int i = start; i < end2; i++)
        {
            string marker = i == _currentLine ? ">" : " ";
            _terminal.WriteLine($"{marker}{i + 1,5}: {_lines[i]}");
        }
    }

    private void NumberedList(string param)
    {
        ListLines(param);
    }

    private void TypeLine(string param)
    {
        if (_lines.Count == 0) return;
        int line = _currentLine;
        if (!string.IsNullOrEmpty(param) && int.TryParse(param, out int n))
            line = n - 1;
        if (line >= 0 && line < _lines.Count)
        {
            _terminal.WriteLine(_lines[line]);
            _currentLine = line;
        }
        else
            _terminal.WriteLine("?");
    }

    private void Search(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) { _terminal.WriteLine("?"); return; }
        for (int i = _currentLine; i < _lines.Count; i++)
        {
            if (_lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _currentLine = i;
                _terminal.WriteLine($"{i + 1}: {_lines[i]}");
                return;
            }
        }
        _terminal.WriteLine("Not found");
    }

    private void Replace(string param)
    {
        // R/old/new/ - replace in current line
        if (param.Length < 4) { _terminal.WriteLine("R/old/new/"); return; }
        char delim = param[0];
        var parts = param[1..].Split(delim);
        if (parts.Length < 2) { _terminal.WriteLine("?"); return; }

        string oldText = parts[0];
        string newText = parts[1];
        bool all = parts.Length > 2 && parts[2].Contains('*');

        if (all)
        {
            int count = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].Contains(oldText))
                {
                    _lines[i] = _lines[i].Replace(oldText, newText);
                    count++;
                }
            }
            _terminal.WriteLine($"{count} replacements");
        }
        else if (_currentLine < _lines.Count)
        {
            _lines[_currentLine] = _lines[_currentLine].Replace(oldText, newText);
            _terminal.WriteLine($"{_currentLine + 1}: {_lines[_currentLine]}");
        }
        _modified = true;
    }

    private void Goto(string param)
    {
        if (int.TryParse(param, out int n))
        {
            n--;
            if (n >= 0 && n < _lines.Count)
            {
                _currentLine = n;
                _terminal.WriteLine($"{_currentLine + 1}: {_lines[_currentLine]}");
            }
            else
                _terminal.WriteLine("?");
        }
        else
            _terminal.WriteLine($"Line {_currentLine + 1} of {_lines.Count}");
    }

    private void Page(string param)
    {
        int count = 23;
        if (!string.IsNullOrEmpty(param) && int.TryParse(param, out int n))
            count = n;

        int end = Math.Min(_lines.Count, _currentLine + count);
        for (int i = _currentLine; i < end; i++)
            _terminal.WriteLine($"{i + 1,5}: {_lines[i]}");
        if (end < _lines.Count)
            _currentLine = end;
    }

    private void Kill(string param)
    {
        // Kill (clear) all lines
        _lines.Clear();
        _currentLine = 0;
        _modified = true;
        _terminal.WriteLine("Buffer cleared");
    }

    private void Append(string param)
    {
        _terminal.WriteLine("Append mode (enter '.' on a line by itself to exit):");
        _currentLine = _lines.Count;
        while (true)
        {
            _terminal.Write($"{_currentLine + 1}: ");
            string line = _terminal.ReadLine();
            if (line == ".") break;
            _lines.Add(line);
            _currentLine++;
            _modified = true;
        }
    }

    private void WriteFile()
    {
        var text = string.Join("\n", _lines);
        _disk.WriteFile(_fileName, text);
        _modified = false;
        _terminal.WriteLine($" : {_lines.Count} lines written to {_fileName}");
    }

    private bool ExitSave()
    {
        WriteFile();
        return true;
    }

    private bool Quit()
    {
        if (_modified)
        {
            _terminal.Write("Discard changes? (Y/N) ");
            string answer = _terminal.ReadLine();
            if (answer.Length == 0 || char.ToUpper(answer[0]) != 'Y')
                return false;
        }
        return true;
    }

    private void Help()
    {
        _terminal.WriteLine("ED Commands:");
        _terminal.WriteLine("  I        - Insert lines (end with '.')");
        _terminal.WriteLine("  I text   - Insert single line");
        _terminal.WriteLine("  A        - Append lines at end");
        _terminal.WriteLine("  D [n]    - Delete n lines");
        _terminal.WriteLine("  L [n]    - List n lines");
        _terminal.WriteLine("  N [n]    - Numbered list");
        _terminal.WriteLine("  P [n]    - Page forward n lines");
        _terminal.WriteLine("  T [n]    - Type line n");
        _terminal.WriteLine("  G n      - Go to line n");
        _terminal.WriteLine("  S text   - Search for text");
        _terminal.WriteLine("  R/o/n/   - Replace old with new");
        _terminal.WriteLine("  R/o/n/*  - Replace all occurrences");
        _terminal.WriteLine("  K        - Kill (clear) buffer");
        _terminal.WriteLine("  W        - Write file");
        _terminal.WriteLine("  E        - Save and exit");
        _terminal.WriteLine("  Q        - Quit (no save)");
        _terminal.WriteLine("  n        - Go to line number n");
        _terminal.WriteLine("  H        - This help");
    }
}
