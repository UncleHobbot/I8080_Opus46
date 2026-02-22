namespace I8080.CpmSystem;

public sealed class VirtualDisk
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _readOnly = new(StringComparer.OrdinalIgnoreCase);
    public byte CurrentDrive { get; set; } // 0=A, 1=B, etc.
    public byte CurrentUser { get; set; }

    public void WriteFile(string name, byte[] data)
    {
        _files[NormalizeName(name)] = data;
        _readOnly[NormalizeName(name)] = false;
    }

    public void WriteFile(string name, string text)
    {
        // CP/M text files use CR/LF and end with Ctrl-Z (0x1A)
        var normalized = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        if (!normalized.EndsWith("\x1A"))
            normalized += "\x1A";
        WriteFile(name, System.Text.Encoding.ASCII.GetBytes(normalized));
    }

    public byte[]? ReadFile(string name)
    {
        _files.TryGetValue(NormalizeName(name), out var data);
        return data;
    }

    public string? ReadFileAsText(string name)
    {
        var data = ReadFile(name);
        if (data == null) return null;
        var text = System.Text.Encoding.ASCII.GetString(data);
        // Strip trailing Ctrl-Z
        int idx = text.IndexOf('\x1A');
        if (idx >= 0) text = text[..idx];
        return text;
    }

    public bool FileExists(string name) => _files.ContainsKey(NormalizeName(name));

    public bool DeleteFile(string name) => _files.Remove(NormalizeName(name));

    public bool RenameFile(string oldName, string newName)
    {
        var key = NormalizeName(oldName);
        if (!_files.TryGetValue(key, out var data)) return false;
        _files.Remove(key);
        _files[NormalizeName(newName)] = data;
        return true;
    }

    public IEnumerable<string> GetFiles(string pattern = "*.*")
    {
        var (namePattern, extPattern) = ParsePattern(pattern);
        foreach (var key in _files.Keys)
        {
            var parts = key.Split('.');
            var n = parts[0];
            var e = parts.Length > 1 ? parts[1] : "";
            if (MatchWildcard(n, namePattern) && MatchWildcard(e, extPattern))
                yield return key;
        }
    }

    public int GetFileSize(string name)
    {
        var data = ReadFile(name);
        return data?.Length ?? 0;
    }

    private static string NormalizeName(string name)
    {
        name = name.Trim().ToUpperInvariant();
        if (!name.Contains('.')) name += ".";
        return name;
    }

    private static (string name, string ext) ParsePattern(string pattern)
    {
        var parts = pattern.ToUpperInvariant().Split('.');
        return (parts[0], parts.Length > 1 ? parts[1] : "*");
    }

    private static bool MatchWildcard(string text, string pattern)
    {
        if (pattern == "*" || pattern == "?*") return true;
        int ti = 0, pi = 0;
        int starTi = -1, starPi = -1;
        while (ti < text.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == '?' || pattern[pi] == text[ti]))
            { ti++; pi++; }
            else if (pi < pattern.Length && pattern[pi] == '*')
            { starPi = pi++; starTi = ti; }
            else if (starPi >= 0)
            { pi = starPi + 1; ti = ++starTi; }
            else return false;
        }
        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        return pi == pattern.Length;
    }
}
