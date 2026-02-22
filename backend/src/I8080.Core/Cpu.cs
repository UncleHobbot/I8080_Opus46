namespace I8080.Core;

public sealed class Cpu
{
    public Registers Reg { get; } = new();
    public Memory Memory { get; } = new();
    public IIOBus IO { get; set; } = new NullIOBus();
    public bool Halted { get; set; }
    public bool InterruptsEnabled { get; set; }
    public long CycleCount { get; private set; }

    // Hook: return true to intercept the call (skip normal execution)
    public Func<ushort, Cpu, bool>? CallInterceptor { get; set; }
    public Func<byte, Cpu, bool>? RstInterceptor { get; set; }

    private static readonly bool[] ParityTable = BuildParityTable();

    private static bool[] BuildParityTable()
    {
        var table = new bool[256];
        for (int i = 0; i < 256; i++)
        {
            int bits = 0;
            int v = i;
            while (v != 0) { bits += v & 1; v >>= 1; }
            table[i] = (bits & 1) == 0;
        }
        return table;
    }

    public void Reset()
    {
        Reg.Reset();
        Memory.Clear();
        Halted = false;
        InterruptsEnabled = false;
        CycleCount = 0;
    }

    private byte NextByte()
    {
        byte b = Memory.Read(Reg.PC);
        Reg.PC++;
        return b;
    }

    private ushort NextWord()
    {
        byte lo = NextByte();
        byte hi = NextByte();
        return (ushort)((hi << 8) | lo);
    }

    private void Push(ushort value)
    {
        Reg.SP -= 2;
        Memory.WriteWord(Reg.SP, value);
    }

    private ushort Pop()
    {
        ushort val = Memory.ReadWord(Reg.SP);
        Reg.SP += 2;
        return val;
    }

    private void SetFlag(CpuFlags flag, bool value)
    {
        if (value) Reg.Flags |= flag;
        else Reg.Flags &= ~flag;
    }

    private bool GetFlag(CpuFlags flag) => Reg.Flags.HasFlag(flag);

    private void UpdateZSP(byte result)
    {
        SetFlag(CpuFlags.Zero, result == 0);
        SetFlag(CpuFlags.Sign, (result & 0x80) != 0);
        SetFlag(CpuFlags.Parity, ParityTable[result]);
    }

    private byte GetReg(int index) => index switch
    {
        0 => Reg.B, 1 => Reg.C, 2 => Reg.D, 3 => Reg.E,
        4 => Reg.H, 5 => Reg.L, 6 => Memory.Read(Reg.HL), 7 => Reg.A,
        _ => 0
    };

    private void SetReg(int index, byte value)
    {
        switch (index)
        {
            case 0: Reg.B = value; break;
            case 1: Reg.C = value; break;
            case 2: Reg.D = value; break;
            case 3: Reg.E = value; break;
            case 4: Reg.H = value; break;
            case 5: Reg.L = value; break;
            case 6: Memory.Write(Reg.HL, value); break;
            case 7: Reg.A = value; break;
        }
    }

    private ushort GetRP(int index) => index switch
    {
        0 => Reg.BC, 1 => Reg.DE, 2 => Reg.HL, 3 => Reg.SP,
        _ => 0
    };

    private void SetRP(int index, ushort value)
    {
        switch (index)
        {
            case 0: Reg.BC = value; break;
            case 1: Reg.DE = value; break;
            case 2: Reg.HL = value; break;
            case 3: Reg.SP = value; break;
        }
    }

    private byte Add(byte a, byte b, bool carry = false)
    {
        int c = carry && GetFlag(CpuFlags.Carry) ? 1 : 0;
        int result = a + b + c;
        SetFlag(CpuFlags.Carry, result > 0xFF);
        SetFlag(CpuFlags.AuxCarry, ((a & 0x0F) + (b & 0x0F) + c) > 0x0F);
        byte r = (byte)result;
        UpdateZSP(r);
        return r;
    }

    private byte Sub(byte a, byte b, bool borrow = false)
    {
        int c = borrow && GetFlag(CpuFlags.Carry) ? 1 : 0;
        int result = a - b - c;
        SetFlag(CpuFlags.Carry, result < 0);
        SetFlag(CpuFlags.AuxCarry, ((a & 0x0F) - (b & 0x0F) - c) < 0);
        byte r = (byte)result;
        UpdateZSP(r);
        return r;
    }

    private void Ana(byte value)
    {
        // AuxCarry is set to the OR of bit 3 of the operands
        SetFlag(CpuFlags.AuxCarry, ((Reg.A | value) & 0x08) != 0);
        Reg.A &= value;
        SetFlag(CpuFlags.Carry, false);
        UpdateZSP(Reg.A);
    }

    private void Xra(byte value)
    {
        Reg.A ^= value;
        SetFlag(CpuFlags.Carry, false);
        SetFlag(CpuFlags.AuxCarry, false);
        UpdateZSP(Reg.A);
    }

    private void Ora(byte value)
    {
        Reg.A |= value;
        SetFlag(CpuFlags.Carry, false);
        SetFlag(CpuFlags.AuxCarry, false);
        UpdateZSP(Reg.A);
    }

    private void Cmp(byte value)
    {
        int result = Reg.A - value;
        SetFlag(CpuFlags.Carry, result < 0);
        SetFlag(CpuFlags.AuxCarry, ((Reg.A & 0x0F) - (value & 0x0F)) < 0);
        UpdateZSP((byte)result);
    }

    private bool EvalCondition(int cc) => cc switch
    {
        0 => !GetFlag(CpuFlags.Zero),     // NZ
        1 => GetFlag(CpuFlags.Zero),      // Z
        2 => !GetFlag(CpuFlags.Carry),    // NC
        3 => GetFlag(CpuFlags.Carry),     // C
        4 => !GetFlag(CpuFlags.Parity),   // PO
        5 => GetFlag(CpuFlags.Parity),    // PE
        6 => !GetFlag(CpuFlags.Sign),     // P
        7 => GetFlag(CpuFlags.Sign),      // M
        _ => false
    };

    public int Step()
    {
        if (Halted) return 4;

        byte opcode = NextByte();
        int cycles = Execute(opcode);
        CycleCount += cycles;
        return cycles;
    }

    private int Execute(byte opcode)
    {
        switch (opcode)
        {
            case 0x00: return 4;  // NOP
            case 0x08: return 4;  // NOP (undocumented)
            case 0x10: return 4;  // NOP (undocumented)
            case 0x18: return 4;  // NOP (undocumented)
            case 0x20: return 4;  // NOP (undocumented)
            case 0x28: return 4;  // NOP (undocumented)
            case 0x30: return 4;  // NOP (undocumented)
            case 0x38: return 4;  // NOP (undocumented)

            // LXI rp, d16
            case 0x01: Reg.BC = NextWord(); return 10;
            case 0x11: Reg.DE = NextWord(); return 10;
            case 0x21: Reg.HL = NextWord(); return 10;
            case 0x31: Reg.SP = NextWord(); return 10;

            // STAX / LDAX
            case 0x02: Memory.Write(Reg.BC, Reg.A); return 7;  // STAX B
            case 0x12: Memory.Write(Reg.DE, Reg.A); return 7;  // STAX D
            case 0x0A: Reg.A = Memory.Read(Reg.BC); return 7;  // LDAX B
            case 0x1A: Reg.A = Memory.Read(Reg.DE); return 7;  // LDAX D

            // STA / LDA
            case 0x32: Memory.Write(NextWord(), Reg.A); return 13;  // STA
            case 0x3A: Reg.A = Memory.Read(NextWord()); return 13;  // LDA

            // SHLD / LHLD
            case 0x22: Memory.WriteWord(NextWord(), Reg.HL); return 16;  // SHLD
            case 0x2A: Reg.HL = Memory.ReadWord(NextWord()); return 16;  // LHLD

            // INX / DCX
            case 0x03: Reg.BC++; return 5;
            case 0x13: Reg.DE++; return 5;
            case 0x23: Reg.HL++; return 5;
            case 0x33: Reg.SP++; return 5;
            case 0x0B: Reg.BC--; return 5;
            case 0x1B: Reg.DE--; return 5;
            case 0x2B: Reg.HL--; return 5;
            case 0x3B: Reg.SP--; return 5;

            // DAD rp
            case 0x09: { int r = Reg.HL + Reg.BC; SetFlag(CpuFlags.Carry, r > 0xFFFF); Reg.HL = (ushort)r; return 10; }
            case 0x19: { int r = Reg.HL + Reg.DE; SetFlag(CpuFlags.Carry, r > 0xFFFF); Reg.HL = (ushort)r; return 10; }
            case 0x29: { int r = Reg.HL + Reg.HL; SetFlag(CpuFlags.Carry, r > 0xFFFF); Reg.HL = (ushort)r; return 10; }
            case 0x39: { int r = Reg.HL + Reg.SP; SetFlag(CpuFlags.Carry, r > 0xFFFF); Reg.HL = (ushort)r; return 10; }

            // INR / DCR
            case 0x04: Reg.B = Inr(Reg.B); return 5;
            case 0x0C: Reg.C = Inr(Reg.C); return 5;
            case 0x14: Reg.D = Inr(Reg.D); return 5;
            case 0x1C: Reg.E = Inr(Reg.E); return 5;
            case 0x24: Reg.H = Inr(Reg.H); return 5;
            case 0x2C: Reg.L = Inr(Reg.L); return 5;
            case 0x34: Memory.Write(Reg.HL, Inr(Memory.Read(Reg.HL))); return 10;
            case 0x3C: Reg.A = Inr(Reg.A); return 5;
            case 0x05: Reg.B = Dcr(Reg.B); return 5;
            case 0x0D: Reg.C = Dcr(Reg.C); return 5;
            case 0x15: Reg.D = Dcr(Reg.D); return 5;
            case 0x1D: Reg.E = Dcr(Reg.E); return 5;
            case 0x25: Reg.H = Dcr(Reg.H); return 5;
            case 0x2D: Reg.L = Dcr(Reg.L); return 5;
            case 0x35: Memory.Write(Reg.HL, Dcr(Memory.Read(Reg.HL))); return 10;
            case 0x3D: Reg.A = Dcr(Reg.A); return 5;

            // MVI r, d8
            case 0x06: Reg.B = NextByte(); return 7;
            case 0x0E: Reg.C = NextByte(); return 7;
            case 0x16: Reg.D = NextByte(); return 7;
            case 0x1E: Reg.E = NextByte(); return 7;
            case 0x26: Reg.H = NextByte(); return 7;
            case 0x2E: Reg.L = NextByte(); return 7;
            case 0x36: Memory.Write(Reg.HL, NextByte()); return 10;
            case 0x3E: Reg.A = NextByte(); return 7;

            // Rotate
            case 0x07: // RLC
            {
                bool hi = (Reg.A & 0x80) != 0;
                Reg.A = (byte)((Reg.A << 1) | (hi ? 1 : 0));
                SetFlag(CpuFlags.Carry, hi);
                return 4;
            }
            case 0x0F: // RRC
            {
                bool lo = (Reg.A & 0x01) != 0;
                Reg.A = (byte)((Reg.A >> 1) | (lo ? 0x80 : 0));
                SetFlag(CpuFlags.Carry, lo);
                return 4;
            }
            case 0x17: // RAL
            {
                bool hi = (Reg.A & 0x80) != 0;
                Reg.A = (byte)((Reg.A << 1) | (GetFlag(CpuFlags.Carry) ? 1 : 0));
                SetFlag(CpuFlags.Carry, hi);
                return 4;
            }
            case 0x1F: // RAR
            {
                bool lo = (Reg.A & 0x01) != 0;
                Reg.A = (byte)((Reg.A >> 1) | (GetFlag(CpuFlags.Carry) ? 0x80 : 0));
                SetFlag(CpuFlags.Carry, lo);
                return 4;
            }

            // DAA
            case 0x27:
            {
                byte a = Reg.A;
                bool cy = GetFlag(CpuFlags.Carry);
                byte correction = 0;
                if (GetFlag(CpuFlags.AuxCarry) || (a & 0x0F) > 9)
                    correction = 0x06;
                if (cy || a > 0x99 || (a > 0x8F && (a & 0x0F) > 9))
                {
                    correction |= 0x60;
                    cy = true;
                }
                SetFlag(CpuFlags.AuxCarry, ((a & 0x0F) + (correction & 0x0F)) > 0x0F);
                Reg.A = (byte)(a + correction);
                SetFlag(CpuFlags.Carry, cy);
                UpdateZSP(Reg.A);
                return 4;
            }

            // CMA, STC, CMC
            case 0x2F: Reg.A = (byte)~Reg.A; return 4;  // CMA
            case 0x37: SetFlag(CpuFlags.Carry, true); return 4;  // STC
            case 0x3F: SetFlag(CpuFlags.Carry, !GetFlag(CpuFlags.Carry)); return 4;  // CMC

            // MOV (0x40-0x7F except 0x76 which is HLT)
            case 0x76: Halted = true; return 7;  // HLT
            case >= 0x40 and <= 0x7F:
            {
                int dst = (opcode >> 3) & 7;
                int src = opcode & 7;
                SetReg(dst, GetReg(src));
                return (src == 6 || dst == 6) ? 7 : 5;
            }

            // ADD / ADC / SUB / SBB / ANA / XRA / ORA / CMP
            case >= 0x80 and <= 0x87: Reg.A = Add(Reg.A, GetReg(opcode & 7)); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0x88 and <= 0x8F: Reg.A = Add(Reg.A, GetReg(opcode & 7), true); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0x90 and <= 0x97: Reg.A = Sub(Reg.A, GetReg(opcode & 7)); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0x98 and <= 0x9F: Reg.A = Sub(Reg.A, GetReg(opcode & 7), true); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0xA0 and <= 0xA7: Ana(GetReg(opcode & 7)); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0xA8 and <= 0xAF: Xra(GetReg(opcode & 7)); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0xB0 and <= 0xB7: Ora(GetReg(opcode & 7)); return (opcode & 7) == 6 ? 7 : 4;
            case >= 0xB8 and <= 0xBF: Cmp(GetReg(opcode & 7)); return (opcode & 7) == 6 ? 7 : 4;

            // Immediate arithmetic
            case 0xC6: Reg.A = Add(Reg.A, NextByte()); return 7;          // ADI
            case 0xCE: Reg.A = Add(Reg.A, NextByte(), true); return 7;    // ACI
            case 0xD6: Reg.A = Sub(Reg.A, NextByte()); return 7;          // SUI
            case 0xDE: Reg.A = Sub(Reg.A, NextByte(), true); return 7;    // SBI
            case 0xE6: Ana(NextByte()); return 7;                          // ANI
            case 0xEE: Xra(NextByte()); return 7;                          // XRI
            case 0xF6: Ora(NextByte()); return 7;                          // ORI
            case 0xFE: Cmp(NextByte()); return 7;                          // CPI

            // RET / conditional RET
            case 0xC9: Reg.PC = Pop(); return 10;  // RET
            case 0xD9: Reg.PC = Pop(); return 10;  // RET (undocumented)
            case 0xC0: if (!GetFlag(CpuFlags.Zero)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xC8: if (GetFlag(CpuFlags.Zero)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xD0: if (!GetFlag(CpuFlags.Carry)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xD8: if (GetFlag(CpuFlags.Carry)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xE0: if (!GetFlag(CpuFlags.Parity)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xE8: if (GetFlag(CpuFlags.Parity)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xF0: if (!GetFlag(CpuFlags.Sign)) { Reg.PC = Pop(); return 11; } return 5;
            case 0xF8: if (GetFlag(CpuFlags.Sign)) { Reg.PC = Pop(); return 11; } return 5;

            // JMP / conditional JMP
            case 0xC3: Reg.PC = NextWord(); return 10;  // JMP
            case 0xCB: Reg.PC = NextWord(); return 10;  // JMP (undocumented)
            case 0xC2: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Zero)) Reg.PC = a; return 10; }
            case 0xCA: { ushort a = NextWord(); if (GetFlag(CpuFlags.Zero)) Reg.PC = a; return 10; }
            case 0xD2: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Carry)) Reg.PC = a; return 10; }
            case 0xDA: { ushort a = NextWord(); if (GetFlag(CpuFlags.Carry)) Reg.PC = a; return 10; }
            case 0xE2: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Parity)) Reg.PC = a; return 10; }
            case 0xEA: { ushort a = NextWord(); if (GetFlag(CpuFlags.Parity)) Reg.PC = a; return 10; }
            case 0xF2: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Sign)) Reg.PC = a; return 10; }
            case 0xFA: { ushort a = NextWord(); if (GetFlag(CpuFlags.Sign)) Reg.PC = a; return 10; }

            // CALL / conditional CALL
            case 0xCD: // CALL
            {
                ushort addr = NextWord();
                if (CallInterceptor != null && CallInterceptor(addr, this))
                    return 17;
                Push(Reg.PC);
                Reg.PC = addr;
                return 17;
            }
            case 0xDD: goto case 0xCD; // CALL (undocumented)
            case 0xED: goto case 0xCD; // CALL (undocumented)
            case 0xFD: goto case 0xCD; // CALL (undocumented)
            case 0xC4: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Zero)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xCC: { ushort a = NextWord(); if (GetFlag(CpuFlags.Zero)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xD4: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Carry)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xDC: { ushort a = NextWord(); if (GetFlag(CpuFlags.Carry)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xE4: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Parity)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xEC: { ushort a = NextWord(); if (GetFlag(CpuFlags.Parity)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xF4: { ushort a = NextWord(); if (!GetFlag(CpuFlags.Sign)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }
            case 0xFC: { ushort a = NextWord(); if (GetFlag(CpuFlags.Sign)) { if (CallInterceptor != null && CallInterceptor(a, this)) return 17; Push(Reg.PC); Reg.PC = a; return 17; } return 11; }

            // RST
            case 0xC7: case 0xCF: case 0xD7: case 0xDF:
            case 0xE7: case 0xEF: case 0xF7: case 0xFF:
            {
                byte n = (byte)((opcode >> 3) & 7);
                ushort addr = (ushort)(n * 8);
                if (RstInterceptor != null && RstInterceptor(n, this))
                    return 11;
                Push(Reg.PC);
                Reg.PC = addr;
                return 11;
            }

            // PUSH / POP
            case 0xC5: Push(Reg.BC); return 11;
            case 0xD5: Push(Reg.DE); return 11;
            case 0xE5: Push(Reg.HL); return 11;
            case 0xF5: Push((ushort)((Reg.A << 8) | Reg.PSW)); return 11;
            case 0xC1: Reg.BC = Pop(); return 10;
            case 0xD1: Reg.DE = Pop(); return 10;
            case 0xE1: Reg.HL = Pop(); return 10;
            case 0xF1: { ushort v = Pop(); Reg.A = (byte)(v >> 8); Reg.PSW = (byte)(v & 0xFF); return 10; }

            // PCHL, SPHL, XTHL, XCHG
            case 0xE9: Reg.PC = Reg.HL; return 5;   // PCHL
            case 0xF9: Reg.SP = Reg.HL; return 5;   // SPHL
            case 0xE3: // XTHL
            {
                ushort tmp = Memory.ReadWord(Reg.SP);
                Memory.WriteWord(Reg.SP, Reg.HL);
                Reg.HL = tmp;
                return 18;
            }
            case 0xEB: // XCHG
            {
                (Reg.HL, Reg.DE) = (Reg.DE, Reg.HL);
                return 5;
            }

            // I/O
            case 0xDB: Reg.A = IO.In(NextByte()); return 10;  // IN
            case 0xD3: IO.Out(NextByte(), Reg.A); return 10;  // OUT

            // Interrupt control
            case 0xFB: InterruptsEnabled = true; return 4;   // EI
            case 0xF3: InterruptsEnabled = false; return 4;  // DI

            default: return 4; // Unknown opcode treated as NOP
        }
    }

    private byte Inr(byte val)
    {
        SetFlag(CpuFlags.AuxCarry, (val & 0x0F) == 0x0F);
        val++;
        UpdateZSP(val);
        return val;
    }

    private byte Dcr(byte val)
    {
        SetFlag(CpuFlags.AuxCarry, (val & 0x0F) == 0x00);
        val--;
        UpdateZSP(val);
        return val;
    }

    public void Interrupt(byte opcode)
    {
        if (!InterruptsEnabled) return;
        InterruptsEnabled = false;
        Halted = false;
        // Execute the interrupt instruction (typically RST n)
        Execute(opcode);
    }
}
