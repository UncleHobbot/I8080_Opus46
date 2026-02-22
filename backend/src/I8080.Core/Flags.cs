namespace I8080.Core;

[Flags]
public enum CpuFlags : byte
{
    None = 0,
    Carry = 0x01,      // bit 0
    Parity = 0x04,     // bit 2
    AuxCarry = 0x10,   // bit 4
    Zero = 0x40,       // bit 6
    Sign = 0x80        // bit 7
}
