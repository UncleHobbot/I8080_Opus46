using I8080.Core;

namespace I8080.CpmSystem;

/// <summary>
/// CP/M 2.2 BIOS (Basic Input/Output System) emulation.
/// BIOS entry points are at the top of memory and are intercepted.
/// </summary>
public sealed class Bios
{
    private readonly Cpu _cpu;
    private readonly ITerminal _terminal;
    private readonly VirtualDisk _disk;

    // BIOS entry points (starting at BIOS_BASE)
    public const ushort BIOS_BASE = 0xFE00;

    public const ushort BOOT   = BIOS_BASE + 0x00;  // Cold boot
    public const ushort WBOOT  = BIOS_BASE + 0x03;  // Warm boot
    public const ushort CONST  = BIOS_BASE + 0x06;  // Console status
    public const ushort CONIN  = BIOS_BASE + 0x09;  // Console input
    public const ushort CONOUT = BIOS_BASE + 0x0C;  // Console output
    public const ushort LIST   = BIOS_BASE + 0x0F;  // List output
    public const ushort PUNCH  = BIOS_BASE + 0x12;  // Punch output
    public const ushort READER = BIOS_BASE + 0x15;  // Reader input
    public const ushort HOME   = BIOS_BASE + 0x18;  // Home disk
    public const ushort SELDSK = BIOS_BASE + 0x1B;  // Select disk
    public const ushort SETTRK = BIOS_BASE + 0x1E;  // Set track
    public const ushort SETSEC = BIOS_BASE + 0x21;  // Set sector
    public const ushort SETDMA = BIOS_BASE + 0x24;  // Set DMA
    public const ushort READ   = BIOS_BASE + 0x27;  // Read sector
    public const ushort WRITE  = BIOS_BASE + 0x2A;  // Write sector

    public Bios(Cpu cpu, ITerminal terminal, VirtualDisk disk)
    {
        _cpu = cpu;
        _terminal = terminal;
        _disk = disk;
    }

    /// <summary>
    /// Install BIOS jump table in memory (RET instructions at each entry point).
    /// The actual handling is done via CallInterceptor.
    /// </summary>
    public void Install()
    {
        // Write RET (0xC9) at each BIOS entry point so if we miss an intercept, it just returns
        for (ushort addr = BIOS_BASE; addr < 0xFFFF; addr += 3)
        {
            _cpu.Memory.Write(addr, 0xC9); // RET
            _cpu.Memory.Write((ushort)(addr + 1), 0x00);
            _cpu.Memory.Write((ushort)(addr + 2), 0x00);
        }
    }

    public bool HandleCall(ushort address)
    {
        if (address < BIOS_BASE) return false;

        switch (address)
        {
            case BOOT:
            case WBOOT:
                return true; // Handled by CpmMachine

            case CONST:
                _cpu.Reg.A = (byte)(_terminal.KeyAvailable ? 0xFF : 0x00);
                return true;

            case CONIN:
                _cpu.Reg.A = (byte)_terminal.Read();
                return true;

            case CONOUT:
                _terminal.Write((char)_cpu.Reg.C);
                return true;

            case LIST:
            case PUNCH:
                return true; // Discard

            case READER:
                _cpu.Reg.A = 0x1A; // EOF
                return true;

            case HOME:
            case SELDSK:
            case SETTRK:
            case SETSEC:
            case SETDMA:
                _cpu.Reg.A = 0;
                _cpu.Reg.HL = 0;
                return true;

            case READ:
            case WRITE:
                _cpu.Reg.A = 0; // Success
                return true;

            default:
                if (address >= BIOS_BASE)
                    return true; // Unknown BIOS call - just return
                return false;
        }
    }
}
