using I8080.Core;

namespace I8080.CpmSystem;

/// <summary>
/// Complete CP/M 2.2 machine: 8080 CPU + memory + BIOS + BDOS + CCP + virtual disk.
/// </summary>
public sealed class CpmMachine
{
    public Cpu Cpu { get; }
    public VirtualDisk Disk { get; }
    public Bios Bios { get; }
    public Bdos Bdos { get; }
    public Ccp Ccp { get; }
    public ITerminal Terminal { get; }

    // BDOS entry point
    public const ushort BDOS_ENTRY = 0x0005;
    // TPA start
    public const ushort TPA_BASE = 0x0100;
    // BDOS location
    public const ushort BDOS_BASE = 0xEC00;

    private readonly Dictionary<string, Action<string>> _transientPrograms = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _running;

    public CpmMachine(ITerminal terminal)
    {
        Terminal = terminal;
        Cpu = new Cpu();
        Disk = new VirtualDisk();
        Bios = new Bios(Cpu, terminal, Disk);
        Bdos = new Bdos(Cpu, terminal, Disk);
        Ccp = new Ccp(terminal, Disk, LoadAndRunTransient);

        SetupInterceptors();
        SetupMemory();
    }

    private void SetupInterceptors()
    {
        Cpu.CallInterceptor = (address, cpu) =>
        {
            if (address == BDOS_ENTRY)
            {
                Bdos.HandleCall();
                // Simulate RET
                cpu.Reg.PC = Cpu.Memory.ReadWord(cpu.Reg.SP);
                cpu.Reg.SP += 2;
                return true;
            }

            if (address == 0x0000)
            {
                // Warm boot - return to CCP
                cpu.Halted = true;
                return true;
            }

            return Bios.HandleCall(address);
        };
    }

    private void SetupMemory()
    {
        Cpu.Memory.Clear();
        Bios.Install();

        // Set up page 0
        // JMP BIOS_WBOOT at address 0
        Cpu.Memory.Write(0x0000, 0xC3); // JMP
        Cpu.Memory.Write(0x0001, (byte)(Bios.WBOOT & 0xFF));
        Cpu.Memory.Write(0x0002, (byte)(Bios.WBOOT >> 8));

        // JMP BDOS at address 5
        Cpu.Memory.Write(0x0005, 0xC3); // JMP
        Cpu.Memory.Write(0x0006, (byte)(BDOS_BASE & 0xFF));
        Cpu.Memory.Write(0x0007, (byte)(BDOS_BASE >> 8));

        // Put RET at BDOS_BASE so if somehow we get there, we just return
        Cpu.Memory.Write(BDOS_BASE, 0xC9);
    }

    public void RegisterProgram(string name, Action<string> handler)
    {
        _transientPrograms[name] = handler;
    }

    public void Start()
    {
        _running = true;
        Terminal.WriteLine();
        Terminal.WriteLine("CP/M 2.2 Emulator - Intel 8080");
        Terminal.WriteLine("64K TPA");
        Terminal.WriteLine();
        Ccp.Run();
    }

    public void Stop()
    {
        _running = false;
        Ccp.ShouldExit = true;
    }

    private void LoadAndRunTransient(string command, string args)
    {
        // Check registered programs first
        if (_transientPrograms.TryGetValue(command, out var handler))
        {
            handler(args);
            return;
        }

        // Try to load .COM file from disk
        string fileName = command.Contains('.') ? command : command + ".COM";
        var data = Disk.ReadFile(fileName);
        if (data == null)
        {
            Terminal.WriteLine($"{command}?");
            return;
        }

        RunComFile(data, command, args);
    }

    public void RunComFile(byte[] data, string command, string args)
    {
        // Set up memory for transient program
        SetupMemory(); // Reset page 0

        // Load program at TPA
        Cpu.Memory.Load(TPA_BASE, data);

        // Set up default FCB at 0x005C with first argument
        SetupFcb(0x005C, args);

        // Set up DMA/command tail at 0x0080
        SetupCommandTail(0x0080, args);

        // Set up CPU state
        Cpu.Reg.PC = TPA_BASE;
        Cpu.Reg.SP = BDOS_BASE - 2; // Stack just below BDOS
        Cpu.Halted = false;

        // Push return address 0x0000 (warm boot) onto stack
        Cpu.Reg.SP -= 2;
        Cpu.Memory.WriteWord(Cpu.Reg.SP, 0x0000);

        // Run until halted
        int maxInstructions = 100_000_000; // Safety limit
        while (!Cpu.Halted && maxInstructions-- > 0)
        {
            Cpu.Step();
        }
    }

    private void SetupFcb(ushort address, string args)
    {
        // Clear FCB
        for (int i = 0; i < 36; i++)
            Cpu.Memory.Write((ushort)(address + i), 0);

        if (string.IsNullOrEmpty(args)) return;
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var fileName = parts[0].ToUpperInvariant();
        ParseFileNameToFcb(address, fileName);

        // Second FCB at 0x006C
        if (parts.Length > 1)
        {
            for (int i = 0; i < 36; i++)
                Cpu.Memory.Write((ushort)(0x006C + i), 0);
            ParseFileNameToFcb(0x006C, parts[1].ToUpperInvariant());
        }
    }

    private void ParseFileNameToFcb(ushort address, string fileName)
    {
        byte drive = 0;
        if (fileName.Length >= 2 && fileName[1] == ':')
        {
            drive = (byte)(fileName[0] - 'A' + 1);
            fileName = fileName[2..];
        }
        Cpu.Memory.Write(address, drive);

        var dotIdx = fileName.IndexOf('.');
        var name = dotIdx >= 0 ? fileName[..dotIdx] : fileName;
        var ext = dotIdx >= 0 ? fileName[(dotIdx + 1)..] : "";

        for (int i = 0; i < 8; i++)
            Cpu.Memory.Write((ushort)(address + 1 + i), (byte)(i < name.Length ? name[i] : ' '));
        for (int i = 0; i < 3; i++)
            Cpu.Memory.Write((ushort)(address + 9 + i), (byte)(i < ext.Length ? ext[i] : ' '));
    }

    private void SetupCommandTail(ushort address, string args)
    {
        var tail = string.IsNullOrEmpty(args) ? "" : " " + args.ToUpperInvariant();
        if (tail.Length > 127) tail = tail[..127];
        Cpu.Memory.Write(address, (byte)tail.Length);
        for (int i = 0; i < tail.Length; i++)
            Cpu.Memory.Write((ushort)(address + 1 + i), (byte)tail[i]);
        Cpu.Memory.Write((ushort)(address + 1 + tail.Length), 0);
    }
}
