Model: Opus 4.6. Verdict: fully implemented

# Intel 8080 CP/M Emulator

A complete Intel 8080-based personal computer emulator running CP/M 2.2, with an embedded text editor, assembler, and BASIC interpreter. Backend in .NET 10, frontend in React.

## Architecture

```
┌─────────────────────────────────────────────┐
│  React Frontend (xterm.js terminal)         │
│  ┌───────────────────────────────────────┐  │
│  │  Terminal Emulator (WebSocket/SignalR) │  │
│  └───────────────────────────────────────┘  │
└──────────────────┬──────────────────────────┘
                   │ SignalR
┌──────────────────▼──────────────────────────┐
│  ASP.NET Web API (I8080.Web)                │
│  ┌───────────────────────────────────────┐  │
│  │  TerminalHub (per-session CP/M)       │  │
│  └────────────────┬──────────────────────┘  │
│  ┌────────────────▼──────────────────────┐  │
│  │  CP/M Machine                         │  │
│  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ │  │
│  │  │ BIOS │ │ BDOS │ │ CCP  │ │ Disk │ │  │
│  │  └──┬───┘ └──┬───┘ └──┬───┘ └──────┘ │  │
│  │     └────────┴────────┘               │  │
│  │  ┌────────────────────────────────┐   │  │
│  │  │  Intel 8080 CPU Emulator       │   │  │
│  │  │  (All 256 opcodes, 64KB RAM)   │   │  │
│  │  └────────────────────────────────┘   │  │
│  │  ┌────────────────────────────────┐   │  │
│  │  │  Programs: ED, ASM, MBASIC     │   │  │
│  │  └────────────────────────────────┘   │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

## Projects

| Project | Description |
|---------|-------------|
| `I8080.Core` | Intel 8080 CPU emulator with all opcodes, registers, flags |
| `I8080.CpmSystem` | CP/M 2.2 OS: BIOS, BDOS, CCP, virtual filesystem |
| `I8080.Programs` | Built-in programs: ED editor, ASM assembler, MBASIC interpreter |
| `I8080.Web` | ASP.NET Web API with SignalR terminal hub |
| `frontend` | React app with xterm.js terminal emulator |

## Quick Start

### Prerequisites

- .NET 10 SDK
- Node.js 18+

### Run

**Backend:**
```bash
cd backend/src/I8080.Web
dotnet run
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173 in your browser.

### First Steps

1. Type `HELP` to see available commands
2. Type `DIR` to list files
3. Type `MBASIC` to start BASIC, then `RUN` a program
4. Type `ED HELLO.ASM` to edit assembly, then `ASM HELLO` to assemble

## Documentation

- [Terminal User Guide](docs/terminal-guide.md) - System overview and CP/M commands
- [ED Editor Guide](docs/editor-guide.md) - Text editor reference
- [ASM Assembler Guide](docs/assembler-guide.md) - 8080 assembler and instruction set
- [MBASIC Guide](docs/basic-guide.md) - BASIC interpreter reference

## Features

### Intel 8080 CPU Emulator
- All 256 opcodes (including undocumented NOPs)
- Accurate flag computation (Z, S, P, CY, AC)
- Register pairs (BC, DE, HL, SP, PSW)
- I/O port support
- Interrupt support
- Call/RST interceptors for system integration

### CP/M 2.2 Operating System
- BIOS with console I/O
- BDOS with 35+ system functions
- CCP with built-in commands (DIR, TYPE, ERA, REN, USER)
- Virtual filesystem with wildcard support
- FCB-based sequential and random file access
- Multiple user areas

### ED - Text Editor
- Line-oriented editing
- Insert, delete, search, replace
- Global search and replace
- File load/save
- Numbered listings

### ASM - 8080 Assembler
- Two-pass assembly
- All 8080 mnemonics
- Directives: ORG, EQU, DB, DW, DS, END
- Labels and expressions
- Hex, binary, octal, decimal number formats
- Generates .COM executables and .PRN listings

### MBASIC - BASIC Interpreter
- Interactive and program modes
- PRINT, INPUT, LET, IF/THEN/ELSE, GOTO, GOSUB/RETURN
- FOR/NEXT loops
- DIM arrays, DATA/READ/RESTORE
- String functions (LEFT$, RIGHT$, MID$, etc.)
- Math functions (SIN, COS, SQR, RND, etc.)
- LOAD/SAVE programs to disk

## Technical Details

### Memory Map
```
0000h-00FFh  System page (warm boot, BDOS entry)
0100h-EBFFh  TPA (Transient Program Area)
EC00h-FDFFh  BDOS
FE00h-FFFFh  BIOS
```

### How It Works

The 8080 CPU emulator executes real machine code. CP/M system calls are intercepted:
- `CALL 0005h` → BDOS handler (file I/O, console I/O)
- `JMP 0000h` → Warm boot (return to CCP)
- BIOS addresses → BIOS handlers

Programs (ED, ASM, MBASIC) run as CP/M transient commands registered with the CCP. The assembler produces real 8080 machine code that executes on the emulated CPU.

Each browser session gets its own independent CP/M machine instance with isolated memory and filesystem.
