# ASM - Intel 8080 Assembler Guide

## Overview

ASM is a two-pass assembler for the Intel 8080 processor. It reads `.ASM` source files and produces `.COM` executable files and `.PRN` listing files.

## Usage

```
A>ASM HELLO
```

or

```
A>ASM HELLO.ASM
```

Output:
```
Assembling HELLO.ASM...
HELLO.COM: 25 bytes, 3 symbols
```

The assembler creates:
- `HELLO.COM` - executable binary (loadable at 0100h)
- `HELLO.PRN` - assembly listing with addresses and symbol table

---

## Source Format

Each line has the format:

```
[LABEL:]    MNEMONIC    [OPERANDS]    [; COMMENT]
```

- **Labels** end with `:` or start in column 1
- **Mnemonics** are 8080 instruction names or directives
- **Comments** start with `;`

### Example

```asm
; Simple program
        ORG     0100H           ; Start at TPA
BDOS    EQU     0005H           ; BDOS entry point

START:  MVI     C,9             ; Print string function
        LXI     D,MSG           ; Point to message
        CALL    BDOS            ; Call BDOS
        RET                     ; Return to CP/M

MSG:    DB      'Hello!',0DH,0AH,'$'

        END
```

---

## Directives

| Directive | Description | Example |
|-----------|-------------|---------|
| `ORG addr` | Set origin address | `ORG 0100H` |
| `EQU value` | Define constant | `BDOS EQU 0005H` |
| `DB values` | Define bytes | `DB 'Hello',0DH,0AH` |
| `DW values` | Define words (16-bit) | `DW 1234H, START` |
| `DS count` | Define storage (reserve bytes) | `DS 256` |
| `END` | End of source | `END` |

### DB (Define Byte) Details

`DB` accepts comma-separated values:
- Numbers: `DB 0, 1, 255, 0FFH`
- Characters: `DB 'A'`
- Strings: `DB 'Hello World'`
- Mixed: `DB 'Hi',0DH,0AH,'$'`

---

## Instruction Set

### Data Transfer

| Instruction | Description |
|-------------|-------------|
| `MOV r1,r2` | Copy register r2 to r1 |
| `MVI r,d8` | Load immediate byte into register |
| `LXI rp,d16` | Load immediate word into register pair |
| `LDA addr` | Load A from memory address |
| `STA addr` | Store A to memory address |
| `LHLD addr` | Load HL from memory |
| `SHLD addr` | Store HL to memory |
| `LDAX rp` | Load A from address in rp (B or D) |
| `STAX rp` | Store A to address in rp (B or D) |
| `XCHG` | Exchange DE and HL |

### Arithmetic

| Instruction | Description |
|-------------|-------------|
| `ADD r` | A = A + r |
| `ADC r` | A = A + r + carry |
| `SUB r` | A = A - r |
| `SBB r` | A = A - r - carry |
| `ADI d8` | A = A + immediate |
| `ACI d8` | A = A + immediate + carry |
| `SUI d8` | A = A - immediate |
| `SBI d8` | A = A - immediate - carry |
| `INR r` | Increment register |
| `DCR r` | Decrement register |
| `INX rp` | Increment register pair |
| `DCX rp` | Decrement register pair |
| `DAD rp` | HL = HL + rp |
| `DAA` | Decimal adjust accumulator |

### Logical

| Instruction | Description |
|-------------|-------------|
| `ANA r` | A = A AND r |
| `XRA r` | A = A XOR r |
| `ORA r` | A = A OR r |
| `CMP r` | Compare A with r (set flags) |
| `ANI d8` | A = A AND immediate |
| `XRI d8` | A = A XOR immediate |
| `ORI d8` | A = A OR immediate |
| `CPI d8` | Compare A with immediate |
| `RLC` | Rotate A left |
| `RRC` | Rotate A right |
| `RAL` | Rotate A left through carry |
| `RAR` | Rotate A right through carry |
| `CMA` | Complement A |
| `STC` | Set carry flag |
| `CMC` | Complement carry flag |

### Branch

| Instruction | Description |
|-------------|-------------|
| `JMP addr` | Unconditional jump |
| `JZ/JNZ addr` | Jump if zero / not zero |
| `JC/JNC addr` | Jump if carry / no carry |
| `JP/JM addr` | Jump if positive / minus |
| `JPE/JPO addr` | Jump if parity even / odd |
| `CALL addr` | Call subroutine |
| `CZ/CNZ addr` | Conditional calls (same conditions as jumps) |
| `RET` | Return from subroutine |
| `RZ/RNZ` | Conditional returns |
| `RST n` | Restart (n = 0-7) |

### Stack & I/O

| Instruction | Description |
|-------------|-------------|
| `PUSH rp` | Push register pair onto stack |
| `POP rp` | Pop register pair from stack |
| `PUSH PSW` | Push A and flags |
| `POP PSW` | Pop A and flags |
| `XTHL` | Exchange top of stack with HL |
| `SPHL` | SP = HL |
| `PCHL` | PC = HL (jump to address in HL) |
| `IN port` | Read from I/O port into A |
| `OUT port` | Write A to I/O port |
| `EI` | Enable interrupts |
| `DI` | Disable interrupts |
| `HLT` | Halt processor |
| `NOP` | No operation |

### Registers

- **8-bit:** `A` (accumulator), `B`, `C`, `D`, `E`, `H`, `L`, `M` (memory at HL)
- **16-bit pairs:** `B` (BC), `D` (DE), `H` (HL), `SP` (stack pointer), `PSW` (A + flags)

---

## Number Formats

| Format | Example | Description |
|--------|---------|-------------|
| Decimal | `42` | Default |
| Hexadecimal | `0FFH` or `2AH` | Must start with digit, end with H |
| Binary | `10101010B` | End with B |
| Octal | `377O` or `377Q` | End with O or Q |
| Character | `'A'` | ASCII value |

### Special Symbols

- `$` - current program counter address

### Expressions

Operands support `+` and `-` arithmetic:
```asm
LXI     H,MSG+5
MVI     A,BUFFER-TABLE
```

---

## CP/M BDOS Functions

Programs interact with CP/M through BDOS calls (`CALL 0005H`). Put the function number in register C:

| C | Function | DE Register |
|---|----------|-------------|
| 0 | System Reset | - |
| 1 | Console Input | Returns char in A |
| 2 | Console Output | E = character |
| 9 | Print String | DE = address of '$'-terminated string |
| 10 | Buffered Input | DE = buffer address |
| 11 | Console Status | Returns A = 0/FF |

---

## Complete Example

```asm
; BANNER.ASM - Display a banner
;
        ORG     0100H
;
BDOS    EQU     0005H
;
        MVI     B,3             ; Print 3 times
LOOP:   PUSH    B
        MVI     C,9             ; Print string
        LXI     D,BANNER
        CALL    BDOS
        POP     B
        DCR     B
        JNZ     LOOP
        RET
;
BANNER: DB      '*** CP/M 8080 ***',0DH,0AH,'$'
;
        END
```

Assemble and run:
```
A>ED BANNER.ASM
(type the source, save with E)
A>ASM BANNER
Assembling BANNER.ASM...
BANNER.COM: 28 bytes, 3 symbols
A>BANNER
*** CP/M 8080 ***
*** CP/M 8080 ***
*** CP/M 8080 ***
A>
```
