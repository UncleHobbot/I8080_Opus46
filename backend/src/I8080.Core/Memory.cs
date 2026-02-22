namespace I8080.Core;

public sealed class Memory
{
    private readonly byte[] _ram = new byte[65536];

    public byte Read(ushort address) => _ram[address];

    public void Write(ushort address, byte value) => _ram[address] = value;

    public ushort ReadWord(ushort address)
    {
        byte lo = _ram[address];
        byte hi = _ram[(ushort)(address + 1)];
        return (ushort)((hi << 8) | lo);
    }

    public void WriteWord(ushort address, ushort value)
    {
        _ram[address] = (byte)(value & 0xFF);
        _ram[(ushort)(address + 1)] = (byte)(value >> 8);
    }

    public void Load(ushort address, ReadOnlySpan<byte> data)
    {
        data.CopyTo(_ram.AsSpan(address, data.Length));
    }

    public ReadOnlySpan<byte> Slice(ushort address, int length) =>
        _ram.AsSpan(address, length);

    public void Clear() => Array.Clear(_ram);

    public byte[] GetRawBuffer() => _ram;
}
