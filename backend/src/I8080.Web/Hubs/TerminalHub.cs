using Microsoft.AspNetCore.SignalR;
using I8080.CpmSystem;
using I8080.Programs;

namespace I8080.Web.Hubs;

public sealed class TerminalHub : Hub
{
    private static readonly Dictionary<string, CpmSession> _sessions = new();
    private static readonly Lock _lock = new();
    private readonly IHubContext<TerminalHub> _hubContext;

    public TerminalHub(IHubContext<TerminalHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public override async Task OnConnectedAsync()
    {
        var connId = Context.ConnectionId;
        var session = new CpmSession(connId, text =>
        {
            // Use IHubContext which is safe to call from any thread at any time
            return _hubContext.Clients.Client(connId).SendAsync("output", text);
        });

        lock (_lock)
        {
            _sessions[connId] = session;
        }

        session.Start();
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        lock (_lock)
        {
            if (_sessions.Remove(Context.ConnectionId, out var session))
            {
                session.Stop();
            }
        }
        return base.OnDisconnectedAsync(exception);
    }

    public Task Input(string data)
    {
        CpmSession? session;
        lock (_lock)
        {
            _sessions.TryGetValue(Context.ConnectionId, out session);
        }
        session?.QueueInput(data);
        return Task.CompletedTask;
    }
}

internal sealed class CpmSession
{
    private readonly string _connectionId;
    private readonly Func<string, Task> _sendOutput;
    private readonly BufferedTerminal _terminal;
    private readonly CpmMachine _machine;
    private Thread? _thread;

    public CpmSession(string connectionId, Func<string, Task> sendOutput)
    {
        _connectionId = connectionId;
        _sendOutput = sendOutput;
        _terminal = new BufferedTerminal(text =>
        {
            try
            {
                _sendOutput(text).GetAwaiter().GetResult();
            }
            catch { }
        });
        _machine = new CpmMachine(_terminal);
        ProgramRegistry.RegisterAll(_machine);
    }

    public void Start()
    {
        _thread = new Thread(() =>
        {
            try
            {
                _machine.Start();
            }
            catch (Exception ex)
            {
                try { _sendOutput($"\r\nSystem error: {ex.Message}\r\n").GetAwaiter().GetResult(); }
                catch { }
            }
        })
        {
            IsBackground = true,
            Name = $"CPM-{_connectionId[..8]}"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _machine.Stop();
    }

    public void QueueInput(string data)
    {
        foreach (char c in data)
            _terminal.QueueKey(c);
    }
}
