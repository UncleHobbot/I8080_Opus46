namespace I8080.Core;

public sealed class Registers
{
    public byte A { get; set; }
    public byte B { get; set; }
    public byte C { get; set; }
    public byte D { get; set; }
    public byte E { get; set; }
    public byte H { get; set; }
    public byte L { get; set; }
    public ushort SP { get; set; }
    public ushort PC { get; set; }
    public CpuFlags Flags { get; set; }

    public ushort BC
    {
        get => (ushort)((B << 8) | C);
        set { B = (byte)(value >> 8); C = (byte)(value & 0xFF); }
    }

    public ushort DE
    {
        get => (ushort)((D << 8) | E);
        set { D = (byte)(value >> 8); E = (byte)(value & 0xFF); }
    }

    public ushort HL
    {
        get => (ushort)((H << 8) | L);
        set { H = (byte)(value >> 8); L = (byte)(value & 0xFF); }
    }

    public byte PSW
    {
        get
        {
            byte f = 0x02; // bit 1 is always 1
            if (Flags.HasFlag(CpuFlags.Carry)) f |= 0x01;
            if (Flags.HasFlag(CpuFlags.Parity)) f |= 0x04;
            if (Flags.HasFlag(CpuFlags.AuxCarry)) f |= 0x10;
            if (Flags.HasFlag(CpuFlags.Zero)) f |= 0x40;
            if (Flags.HasFlag(CpuFlags.Sign)) f |= 0x80;
            return f;
        }
        set
        {
            Flags = CpuFlags.None;
            if ((value & 0x01) != 0) Flags |= CpuFlags.Carry;
            if ((value & 0x04) != 0) Flags |= CpuFlags.Parity;
            if ((value & 0x10) != 0) Flags |= CpuFlags.AuxCarry;
            if ((value & 0x40) != 0) Flags |= CpuFlags.Zero;
            if ((value & 0x80) != 0) Flags |= CpuFlags.Sign;
        }
    }

    public void Reset()
    {
        A = B = C = D = E = H = L = 0;
        SP = 0;
        PC = 0;
        Flags = CpuFlags.None;
    }
}
