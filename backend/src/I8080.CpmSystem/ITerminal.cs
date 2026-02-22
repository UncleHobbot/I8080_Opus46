namespace I8080.CpmSystem;

public interface ITerminal
{
    void Write(char c);
    void Write(string text);
    void WriteLine(string text = "");
    char Read();
    bool KeyAvailable { get; }
    string ReadLine();
}

public sealed class BufferedTerminal : ITerminal
{
    private readonly Queue<char> _inputBuffer = new();
    private readonly Action<string> _outputCallback;
    private readonly SemaphoreSlim _inputReady = new(0);

    public BufferedTerminal(Action<string> outputCallback)
    {
        _outputCallback = outputCallback;
    }

    public void Write(char c) => _outputCallback(c.ToString());
    public void Write(string text) => _outputCallback(text);
    public void WriteLine(string text = "") => _outputCallback(text + "\r\n");

    public char Read()
    {
        while (_inputBuffer.Count == 0)
            _inputReady.Wait();
        return _inputBuffer.Dequeue();
    }

    public bool KeyAvailable => _inputBuffer.Count > 0;

    public string ReadLine()
    {
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            char c = Read();
            if (c == '\r' || c == '\n')
            {
                Write("\r\n");
                return sb.ToString();
            }
            if (c == '\b' || c == 127)
            {
                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                    Write("\b \b");
                }
                continue;
            }
            if (c >= 0x20)
            {
                sb.Append(c);
                Write(c);
            }
        }
    }

    public void QueueInput(string text)
    {
        foreach (var c in text)
            _inputBuffer.Enqueue(c);
        _inputReady.Release(text.Length);
    }

    public void QueueKey(char c)
    {
        _inputBuffer.Enqueue(c);
        _inputReady.Release();
    }
}
