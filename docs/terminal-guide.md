# CP/M 2.2 Terminal User Guide

## Getting Started

### Starting the System

1. **Start the backend:**
   ```
   cd backend/src/I8080.Web
   dotnet run
   ```
   The server starts on `http://localhost:5026`.

2. **Start the frontend:**
   ```
   cd frontend
   npm run dev
   ```
   Open `http://localhost:5173` in your browser.

3. The terminal connects automatically and boots into CP/M.

### Boot Sequence

When the system starts, you will see:

```
CP/M 2.2 Emulator - Intel 8080
64K TPA

A>
```

The `A>` prompt indicates you are on Drive A and the Console Command Processor (CCP) is ready for commands.

---

## CP/M Command Reference

### Built-in Commands

| Command | Description | Example |
|---------|-------------|---------|
| `DIR` | List files | `DIR` or `DIR *.ASM` |
| `TYPE` | Display file contents | `TYPE README.TXT` |
| `ERA` | Erase (delete) file | `ERA TEMP.TXT` |
| `REN` | Rename file | `REN NEWNAME.TXT=OLDNAME.TXT` |
| `USER` | Set/show user number | `USER 0` |
| `EXIT` | Exit the emulator | `EXIT` |

### Transient Commands (Programs)

| Command | Description | Example |
|---------|-------------|---------|
| `ED` | Text editor | `ED MYFILE.TXT` |
| `ASM` | 8080 assembler | `ASM HELLO.ASM` |
| `MBASIC` | BASIC interpreter | `MBASIC` or `MBASIC HELLO.BAS` |
| `HELP` | Show system help | `HELP` |

### File Naming

CP/M uses the format `FILENAME.EXT`:
- Filename: up to 8 characters
- Extension: up to 3 characters
- Common extensions: `.TXT` (text), `.ASM` (assembly), `.COM` (executable), `.BAS` (BASIC)

### Wildcards

- `*` matches any characters: `DIR *.ASM` lists all assembly files
- `?` matches a single character: `DIR TEST?.TXT`

---

## Running Programs

### Running a .COM File

If you have assembled a program to a `.COM` file, simply type its name:

```
A>HELLO
Hello from 8080 Assembly!
A>
```

### Workflow Example

1. Write assembly code: `ED HELLO.ASM`
2. Assemble it: `ASM HELLO.ASM`
3. Run the result: `HELLO`

---

## Terminal Controls

| Key | Action |
|-----|--------|
| Enter | Submit command / line |
| Backspace | Delete last character |
| Ctrl+C | Interrupt (in some contexts) |

---

## System Specifications

| Parameter | Value |
|-----------|-------|
| CPU | Intel 8080A @ 2 MHz (emulated) |
| Memory | 64 KB RAM |
| Operating System | CP/M 2.2 |
| TPA (Transient Program Area) | 0100h - EBFFh |
| BDOS Entry | 0005h |
| BIOS Base | FE00h |
| Default DMA | 0080h |

---

## Pre-installed Files

| File | Description |
|------|-------------|
| `HELLO.ASM` | Hello World assembly source |
| `COUNT.ASM` | Count 1-10 assembly source |
| `HELLO.BAS` | Hello World BASIC program |
| `GUESS.BAS` | Number guessing game |
| `FIB.BAS` | Fibonacci sequence generator |
| `README.TXT` | System readme |
