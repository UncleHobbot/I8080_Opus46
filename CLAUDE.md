# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

An Intel 8080 CP/M 2.2 personal computer emulator with built-in programs (ED text editor, ASM assembler, MBASIC interpreter). Backend is .NET 10, frontend is React with xterm.js.

## Build & Run Commands

### Backend
```bash
cd backend/src/I8080.Web
dotnet run                    # Starts on http://localhost:5026
dotnet build                  # Build only (from backend/ directory builds all projects)
```

### Frontend
```bash
cd frontend
npm install                   # First time only
npm run dev                   # Dev server on http://localhost:5173
npm run build                 # Production build (tsc + vite)
npx tsc --noEmit              # Type-check without emitting
```

Vite proxies `/terminal` (WebSocket) and `/api` to the backend at localhost:5026.

## Architecture

```
I8080.Core          ← CPU emulator (standalone, no dependencies)
  ↑
I8080.CpmSystem     ← CP/M OS layer (BIOS, BDOS, CCP, VirtualDisk)
  ↑
I8080.Programs      ← ED, ASM, MBASIC (registered as transient commands)
  ↑
I8080.Web           ← ASP.NET + SignalR hub, one CpmMachine per browser session
```

### Key Integration Pattern

The 8080 CPU runs real machine code. System calls are **intercepted**, not emulated in 8080 code:
- `Cpu.CallInterceptor` catches `CALL 0005h` → routes to `Bdos.HandleCall()` (C# implementation)
- `CALL 0000h` → warm boot (returns to CCP)
- Calls to `FE00h+` → `Bios.HandleCall()` for console/disk I/O

Programs (ED, ASM, MBASIC) are registered via `CpmMachine.RegisterProgram()` and invoked by name from the CCP. They interact with the system through `ITerminal` and `VirtualDisk` interfaces.

### Session Model

Each SignalR connection (`TerminalHub.OnConnectedAsync`) creates an isolated `CpmSession` with its own `CpmMachine` running on a background thread. Input flows: browser → SignalR `Input()` → `BufferedTerminal.QueueKey()` → CCP/program reads. Output flows: program writes to `ITerminal` → `BufferedTerminal` callback → SignalR `output` event → xterm.js.

### Memory Map
```
0000h-00FFh   System page (JMP WBOOT at 0000h, JMP BDOS at 0005h)
0100h-EBFFh   TPA (Transient Program Area, where .COM files load)
EC00h          BDOS base
FE00h          BIOS base
```

## Key Files

- `Cpu.cs` — All 256 opcodes in a single `Execute()` switch. Flags, register pairs, parity table.
- `CpmMachine.cs` — Wires CPU + BIOS + BDOS + CCP + VirtualDisk. Sets up interceptors and memory layout.
- `Bdos.cs` — CP/M BDOS functions 0-35 (console I/O, FCB file operations, DMA).
- `AssemblerProgram.cs` — Two-pass assembler. Pass 1 collects symbols, pass 2 emits bytes. Produces .COM files.
- `BasicInterpreter.cs` — Recursive-descent expression parser, tokenizer, statement executor. Largest file (~1000 LOC).
- `TerminalHub.cs` — SignalR hub with static `Dictionary<string, CpmSession>` for session management.
- `Terminal.tsx` — xterm.js setup, SignalR connection, input/output wiring.

## Conventions

- .NET 10 (`net10.0`), nullable enabled, implicit usings
- Frontend: React 19, TypeScript (strict), Vite 6, ESM modules
- No test framework is configured yet
- No .gitignore exists yet — `bin/`, `obj/`, `node_modules/` should be excluded
- Virtual disk files are created in `ProgramRegistry.InstallSampleFiles()` at session startup
