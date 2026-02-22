namespace I8080.CpmSystem;

/// <summary>
/// CP/M Console Command Processor - handles the command line and built-in commands.
/// </summary>
public sealed class Ccp
{
    private readonly ITerminal _terminal;
    private readonly VirtualDisk _disk;
    private readonly Action<string, string> _loadTransient; // (command, args)

    public bool ShouldExit { get; set; }

    public Ccp(ITerminal terminal, VirtualDisk disk, Action<string, string> loadTransient)
    {
        _terminal = terminal;
        _disk = disk;
        _loadTransient = loadTransient;
    }

    public void Run()
    {
        while (!ShouldExit)
        {
            char drive = (char)('A' + _disk.CurrentDrive);
            _terminal.Write($"{drive}>");
            string line = _terminal.ReadLine().Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToUpperInvariant();
            string args = parts.Length > 1 ? parts[1] : "";

            if (!ExecuteBuiltin(command, args))
            {
                _loadTransient(command, args);
            }
        }
    }

    private bool ExecuteBuiltin(string command, string args)
    {
        switch (command)
        {
            case "DIR":
                Dir(args);
                return true;
            case "TYPE":
                Type(args);
                return true;
            case "ERA":
                Era(args);
                return true;
            case "REN":
                Ren(args);
                return true;
            case "SAVE":
                Save(args);
                return true;
            case "USER":
                User(args);
                return true;
            case "EXIT":
                ShouldExit = true;
                return true;
            default:
                // Check for drive select (e.g., "A:" or "B:")
                if (command.Length == 2 && command[1] == ':' && char.IsLetter(command[0]))
                {
                    _disk.CurrentDrive = (byte)(char.ToUpper(command[0]) - 'A');
                    return true;
                }
                return false;
        }
    }

    private void Dir(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) pattern = "*.*";
        var files = _disk.GetFiles(pattern).ToList();
        if (files.Count == 0)
        {
            _terminal.WriteLine("No file");
            return;
        }

        char drive = (char)('A' + _disk.CurrentDrive);
        int col = 0;
        foreach (var file in files.OrderBy(f => f))
        {
            var parts = file.Split('.');
            var name = parts[0].PadRight(8);
            var ext = (parts.Length > 1 ? parts[1] : "").PadRight(3);
            if (col == 0)
                _terminal.Write($"{drive}: ");
            _terminal.Write($"{name} {ext}  ");
            col++;
            if (col >= 4)
            {
                _terminal.WriteLine();
                col = 0;
            }
        }
        if (col > 0) _terminal.WriteLine();
    }

    private void Type(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            _terminal.WriteLine("Type what?");
            return;
        }
        fileName = NormalizeName(fileName);
        var text = _disk.ReadFileAsText(fileName);
        if (text == null)
        {
            _terminal.WriteLine("No file");
            return;
        }
        _terminal.Write(text.Replace("\r\n", "\r\n"));
        _terminal.WriteLine();
    }

    private void Era(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            _terminal.WriteLine("Era what?");
            return;
        }
        var files = _disk.GetFiles(pattern).ToList();
        if (files.Count == 0)
        {
            _terminal.WriteLine("No file");
            return;
        }
        foreach (var f in files)
            _disk.DeleteFile(f);
    }

    private void Ren(string args)
    {
        // REN newname=oldname
        var parts = args.Split('=');
        if (parts.Length != 2)
        {
            _terminal.WriteLine("Ren what?");
            return;
        }
        var newName = NormalizeName(parts[0].Trim());
        var oldName = NormalizeName(parts[1].Trim());
        if (!_disk.RenameFile(oldName, newName))
            _terminal.WriteLine("No file");
    }

    private void Save(string args)
    {
        _terminal.WriteLine("Save not supported in this mode");
    }

    private void User(string args)
    {
        if (byte.TryParse(args, out byte user) && user < 16)
            _disk.CurrentUser = user;
        else
            _terminal.WriteLine($"User = {_disk.CurrentUser}");
    }

    private static string NormalizeName(string name)
    {
        name = name.Trim().ToUpperInvariant();
        if (!name.Contains('.') && !name.Contains('*'))
            name += ".COM";
        return name;
    }
}
