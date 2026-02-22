using I8080.Core;

namespace I8080.CpmSystem;

/// <summary>
/// CP/M 2.2 BDOS (Basic Disk Operating System) emulation.
/// Handles system calls via CALL 0005.
/// </summary>
public sealed class Bdos
{
    private readonly Cpu _cpu;
    private readonly ITerminal _terminal;
    private readonly VirtualDisk _disk;

    // FCB-based file state for sequential I/O
    private readonly Dictionary<ushort, BdosFileState> _openFiles = new();

    public Bdos(Cpu cpu, ITerminal terminal, VirtualDisk disk)
    {
        _cpu = cpu;
        _terminal = terminal;
        _disk = disk;
    }

    public void HandleCall()
    {
        byte function = _cpu.Reg.C;
        ushort de = _cpu.Reg.DE;

        switch (function)
        {
            case 0: // System Reset
                break;

            case 1: // Console Input (C_READ)
            {
                char c = _terminal.Read();
                _cpu.Reg.A = (byte)c;
                break;
            }

            case 2: // Console Output (C_WRITE)
                _terminal.Write((char)_cpu.Reg.E);
                break;

            case 6: // Direct Console I/O
                if (_cpu.Reg.E == 0xFF)
                {
                    if (_terminal.KeyAvailable)
                    {
                        char c = _terminal.Read();
                        _cpu.Reg.A = (byte)c;
                    }
                    else
                    {
                        _cpu.Reg.A = 0;
                    }
                }
                else
                {
                    _terminal.Write((char)_cpu.Reg.E);
                }
                break;

            case 9: // Print String (C_WRITESTR)
            {
                ushort addr = de;
                while (true)
                {
                    byte b = _cpu.Memory.Read(addr++);
                    if (b == (byte)'$') break;
                    _terminal.Write((char)b);
                }
                break;
            }

            case 10: // Buffered Console Input (C_READSTR)
            {
                byte maxLen = _cpu.Memory.Read(de);
                string line = _terminal.ReadLine();
                if (line.Length > maxLen) line = line[..maxLen];
                _cpu.Memory.Write((ushort)(de + 1), (byte)line.Length);
                for (int i = 0; i < line.Length; i++)
                    _cpu.Memory.Write((ushort)(de + 2 + i), (byte)line[i]);
                break;
            }

            case 11: // Get Console Status (C_STAT)
                _cpu.Reg.A = (byte)(_terminal.KeyAvailable ? 0xFF : 0x00);
                break;

            case 12: // Return Version Number
                _cpu.Reg.A = 0x22; // CP/M 2.2
                _cpu.Reg.H = 0x00;
                _cpu.Reg.L = 0x22;
                break;

            case 13: // Reset Disk System
                _cpu.Reg.A = 0;
                break;

            case 14: // Select Disk
                _disk.CurrentDrive = _cpu.Reg.E;
                _cpu.Reg.A = 0;
                break;

            case 15: // Open File
            {
                var fcb = ReadFcb(de);
                if (_disk.FileExists(fcb.FullName))
                {
                    var state = new BdosFileState { FileName = fcb.FullName, Position = 0 };
                    _openFiles[de] = state;
                    _cpu.Memory.Write((ushort)(de + 32), 0); // CR = 0
                    _cpu.Reg.A = 0; // success (directory code)
                }
                else
                {
                    _cpu.Reg.A = 0xFF; // file not found
                }
                break;
            }

            case 16: // Close File
                _openFiles.Remove(de);
                _cpu.Reg.A = 0;
                break;

            case 17: // Search First
            {
                var fcb = ReadFcb(de);
                var pattern = fcb.FullName;
                var files = _disk.GetFiles(pattern).ToList();
                if (files.Count > 0)
                {
                    WriteFcbAt(0x0080, files[0]); // Write to DMA
                    _searchResults = files;
                    _searchIndex = 1;
                    _cpu.Reg.A = 0;
                }
                else
                {
                    _cpu.Reg.A = 0xFF;
                }
                break;
            }

            case 18: // Search Next
            {
                if (_searchResults != null && _searchIndex < _searchResults.Count)
                {
                    WriteFcbAt(0x0080, _searchResults[_searchIndex++]);
                    _cpu.Reg.A = 0;
                }
                else
                {
                    _cpu.Reg.A = 0xFF;
                }
                break;
            }

            case 19: // Delete File
            {
                var fcb = ReadFcb(de);
                _cpu.Reg.A = (byte)(_disk.DeleteFile(fcb.FullName) ? 0 : 0xFF);
                break;
            }

            case 20: // Read Sequential
            {
                if (_openFiles.TryGetValue(de, out var state))
                {
                    var data = _disk.ReadFile(state.FileName);
                    if (data != null && state.Position < data.Length)
                    {
                        int count = Math.Min(128, data.Length - state.Position);
                        ushort dma = _dmaAddress;
                        for (int i = 0; i < count; i++)
                            _cpu.Memory.Write((ushort)(dma + i), data[state.Position + i]);
                        for (int i = count; i < 128; i++)
                            _cpu.Memory.Write((ushort)(dma + i), 0x1A); // fill with ^Z
                        state.Position += 128;
                        _cpu.Reg.A = 0;
                    }
                    else
                    {
                        _cpu.Reg.A = 1; // EOF
                    }
                }
                else
                {
                    _cpu.Reg.A = 9; // invalid FCB
                }
                break;
            }

            case 21: // Write Sequential
            {
                if (_openFiles.TryGetValue(de, out var state))
                {
                    var existing = _disk.ReadFile(state.FileName) ?? [];
                    var newData = new byte[Math.Max(existing.Length, state.Position + 128)];
                    Array.Copy(existing, newData, existing.Length);
                    ushort dma = _dmaAddress;
                    for (int i = 0; i < 128; i++)
                        newData[state.Position + i] = _cpu.Memory.Read((ushort)(dma + i));
                    _disk.WriteFile(state.FileName, newData);
                    state.Position += 128;
                    _cpu.Reg.A = 0;
                }
                else
                {
                    _cpu.Reg.A = 9;
                }
                break;
            }

            case 22: // Make File
            {
                var fcb = ReadFcb(de);
                _disk.WriteFile(fcb.FullName, []);
                var state = new BdosFileState { FileName = fcb.FullName, Position = 0 };
                _openFiles[de] = state;
                _cpu.Memory.Write((ushort)(de + 32), 0);
                _cpu.Reg.A = 0;
                break;
            }

            case 23: // Rename File
            {
                var oldFcb = ReadFcb(de);
                var newFcb = ReadFcb((ushort)(de + 16));
                _cpu.Reg.A = (byte)(_disk.RenameFile(oldFcb.FullName, newFcb.FullName) ? 0 : 0xFF);
                break;
            }

            case 24: // Return Login Vector
                _cpu.Reg.HL = 0x0001; // Drive A: only
                break;

            case 25: // Return Current Disk
                _cpu.Reg.A = _disk.CurrentDrive;
                break;

            case 26: // Set DMA Address
                _dmaAddress = de;
                break;

            case 32: // Set/Get User Code
                if (_cpu.Reg.E == 0xFF)
                    _cpu.Reg.A = _disk.CurrentUser;
                else
                    _disk.CurrentUser = _cpu.Reg.E;
                break;

            case 33: // Read Random
            {
                if (_openFiles.TryGetValue(de, out var state))
                {
                    byte r0 = _cpu.Memory.Read((ushort)(de + 33));
                    byte r1 = _cpu.Memory.Read((ushort)(de + 34));
                    int record = r0 | (r1 << 8);
                    state.Position = record * 128;
                    var data = _disk.ReadFile(state.FileName);
                    if (data != null && state.Position < data.Length)
                    {
                        int count = Math.Min(128, data.Length - state.Position);
                        ushort dma = _dmaAddress;
                        for (int i = 0; i < count; i++)
                            _cpu.Memory.Write((ushort)(dma + i), data[state.Position + i]);
                        for (int i = count; i < 128; i++)
                            _cpu.Memory.Write((ushort)(dma + i), 0x1A);
                        _cpu.Reg.A = 0;
                    }
                    else
                    {
                        _cpu.Reg.A = 6; // seek past end
                    }
                }
                else
                {
                    _cpu.Reg.A = 9;
                }
                break;
            }

            case 34: // Write Random
            {
                if (_openFiles.TryGetValue(de, out var state))
                {
                    byte r0 = _cpu.Memory.Read((ushort)(de + 33));
                    byte r1 = _cpu.Memory.Read((ushort)(de + 34));
                    int record = r0 | (r1 << 8);
                    state.Position = record * 128;
                    var existing = _disk.ReadFile(state.FileName) ?? [];
                    var newData = new byte[Math.Max(existing.Length, state.Position + 128)];
                    Array.Copy(existing, newData, existing.Length);
                    ushort dma = _dmaAddress;
                    for (int i = 0; i < 128; i++)
                        newData[state.Position + i] = _cpu.Memory.Read((ushort)(dma + i));
                    _disk.WriteFile(state.FileName, newData);
                    _cpu.Reg.A = 0;
                }
                else
                {
                    _cpu.Reg.A = 9;
                }
                break;
            }

            case 35: // Compute File Size
            {
                var fcb = ReadFcb(de);
                int size = _disk.GetFileSize(fcb.FullName);
                int records = (size + 127) / 128;
                _cpu.Memory.Write((ushort)(de + 33), (byte)(records & 0xFF));
                _cpu.Memory.Write((ushort)(de + 34), (byte)((records >> 8) & 0xFF));
                _cpu.Memory.Write((ushort)(de + 35), (byte)((records >> 16) & 0xFF));
                _cpu.Reg.A = 0;
                break;
            }

            default:
                // Unimplemented function - return error
                _cpu.Reg.A = 0xFF;
                break;
        }
    }

    private ushort _dmaAddress = 0x0080;
    private List<string>? _searchResults;
    private int _searchIndex;

    private FcbEntry ReadFcb(ushort address)
    {
        byte drive = _cpu.Memory.Read(address);
        var name = new char[8];
        for (int i = 0; i < 8; i++)
            name[i] = (char)(_cpu.Memory.Read((ushort)(address + 1 + i)) & 0x7F);
        var ext = new char[3];
        for (int i = 0; i < 3; i++)
            ext[i] = (char)(_cpu.Memory.Read((ushort)(address + 9 + i)) & 0x7F);

        var nameStr = new string(name).TrimEnd();
        var extStr = new string(ext).TrimEnd();
        return new FcbEntry(drive, nameStr, extStr);
    }

    private void WriteFcbAt(ushort address, string fileName)
    {
        // Clear 32 bytes
        for (int i = 0; i < 32; i++)
            _cpu.Memory.Write((ushort)(address + i), 0);

        var parts = fileName.Split('.');
        var name = parts[0].PadRight(8);
        var ext = (parts.Length > 1 ? parts[1] : "").PadRight(3);

        _cpu.Memory.Write(address, 0); // current drive
        for (int i = 0; i < 8; i++)
            _cpu.Memory.Write((ushort)(address + 1 + i), (byte)(i < name.Length ? name[i] : ' '));
        for (int i = 0; i < 3; i++)
            _cpu.Memory.Write((ushort)(address + 9 + i), (byte)(i < ext.Length ? ext[i] : ' '));
    }

    private record FcbEntry(byte Drive, string Name, string Ext)
    {
        public string FullName => string.IsNullOrEmpty(Ext) ? Name : $"{Name}.{Ext}";
    }

    private class BdosFileState
    {
        public required string FileName { get; set; }
        public int Position { get; set; }
    }
}
