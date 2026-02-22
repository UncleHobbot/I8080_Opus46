# MBASIC - BASIC Interpreter Guide

## Overview

MBASIC is a BASIC interpreter for CP/M, inspired by Microsoft BASIC. It supports interactive mode (direct commands) and stored programs (numbered lines).

## Starting MBASIC

```
A>MBASIC
MBASIC Ver 5.0
CP/M BASIC Interpreter
Ready
]
```

To load a program on startup:
```
A>MBASIC HELLO.BAS
```

To return to CP/M, type `SYSTEM` or `BYE`.

---

## Interactive Mode

At the `]` prompt, you can:

- **Enter program lines** by starting with a line number:
  ```
  ] 10 PRINT "Hello"
  ] 20 GOTO 10
  ```

- **Execute direct commands** without a line number:
  ```
  ] PRINT 2 + 2
  4
  ```

- **Delete a line** by typing just its number:
  ```
  ] 10
  ```

---

## System Commands

| Command | Description |
|---------|-------------|
| `RUN` | Execute the program |
| `LIST` | List all lines |
| `LIST n` | List line n |
| `LIST n-m` | List lines n through m |
| `NEW` | Clear program and variables |
| `LOAD "file"` | Load a .BAS file |
| `SAVE "file"` | Save program to .BAS file |
| `FILES` | List .BAS files on disk |
| `SYSTEM` | Return to CP/M |
| `BYE` | Same as SYSTEM |

---

## Statements

### Output

**PRINT** (or **?**) - Display values

```basic
10 PRINT "Hello, World!"
20 PRINT "Sum is "; 2 + 3
30 PRINT A; B; C
40 PRINT TAB(10); "Indented"
```

- `;` suppresses newline / no space between items
- `,` adds a tab between items
- Line without `;` or `,` at end prints newline

### Input

**INPUT** - Read from keyboard

```basic
10 INPUT "Your name"; N$
20 INPUT "Enter two numbers"; A, B
30 INPUT X
```

### Assignment

**LET** - Assign a value (LET keyword is optional)

```basic
10 LET X = 42
20 Y = X * 2
30 A$ = "Hello"
```

### Control Flow

**GOTO** - Unconditional jump:
```basic
10 PRINT "Loop"
20 GOTO 10
```

**IF/THEN/ELSE** - Conditional:
```basic
10 IF X > 10 THEN PRINT "Big" ELSE PRINT "Small"
20 IF A$ = "YES" THEN 100
30 IF X <> 0 THEN GOSUB 500
```

**GOSUB/RETURN** - Subroutine call:
```basic
10 GOSUB 100
20 PRINT "Back from sub"
30 END
100 PRINT "In subroutine"
110 RETURN
```

**FOR/NEXT** - Counted loop:
```basic
10 FOR I = 1 TO 10
20 PRINT I;
30 NEXT I

40 FOR J = 10 TO 0 STEP -2
50 PRINT J;
60 NEXT J
```

**END / STOP** - Terminate program

### Data

**DATA/READ/RESTORE**:
```basic
10 DATA 10, 20, 30, "Hello"
20 READ A, B, C, D$
30 PRINT A; B; C; D$
40 RESTORE
50 READ X
```

### Arrays

**DIM** - Declare arrays:
```basic
10 DIM A(100)
20 DIM N$(50)
30 FOR I = 0 TO 100
40 A(I) = I * I
50 NEXT I
```

Arrays default to size 10 if not DIMmed.

### Other

**REM** - Comment:
```basic
10 REM This is a comment
```

**CLS** - Clear screen

**POKE** - Memory poke (ignored in emulator)

---

## Operators

### Arithmetic
| Operator | Description |
|----------|-------------|
| `+` | Addition (or string concatenation) |
| `-` | Subtraction |
| `*` | Multiplication |
| `/` | Division |
| `^` | Exponentiation |
| `MOD` | Modulo |

### Comparison
| Operator | Description |
|----------|-------------|
| `=` | Equal |
| `<>` or `><` | Not equal |
| `<` | Less than |
| `>` | Greater than |
| `<=` | Less than or equal |
| `>=` | Greater than or equal |

### Logical
| Operator | Description |
|----------|-------------|
| `AND` | Logical AND |
| `OR` | Logical OR |
| `NOT` | Logical NOT |

---

## Built-in Functions

### Numeric Functions

| Function | Description |
|----------|-------------|
| `ABS(x)` | Absolute value |
| `INT(x)` | Floor (integer part) |
| `SQR(x)` | Square root |
| `SIN(x)` | Sine (radians) |
| `COS(x)` | Cosine |
| `TAN(x)` | Tangent |
| `ATN(x)` | Arctangent |
| `EXP(x)` | e^x |
| `LOG(x)` | Natural logarithm |
| `RND(x)` | Random number (0 to x) |
| `SGN(x)` | Sign (-1, 0, or 1) |
| `FRE(x)` | Free memory |

### String Functions

| Function | Description |
|----------|-------------|
| `LEN(s$)` | String length |
| `LEFT$(s$,n)` | Left n characters |
| `RIGHT$(s$,n)` | Right n characters |
| `MID$(s$,p,n)` | Substring from position p, length n |
| `CHR$(n)` | Character from ASCII code |
| `ASC(s$)` | ASCII code of first character |
| `STR$(n)` | Convert number to string |
| `VAL(s$)` | Convert string to number |

### Formatting Functions

| Function | Description |
|----------|-------------|
| `TAB(n)` | Move to column n |
| `SPC(n)` | Insert n spaces |

---

## Variables

- **Numeric:** `A`, `X1`, `COUNT` (default value: 0)
- **String:** `A$`, `NAME$` (end with `$`, default: empty)
- **Arrays:** `A(10)`, `N$(5)` (must DIM for sizes > 10)

Variable names are case-insensitive.

---

## Examples

### Hello World
```basic
10 PRINT "Hello, World!"
```

### Temperature Converter
```basic
10 PRINT "Fahrenheit to Celsius Converter"
20 INPUT "Enter degrees F"; F
30 C = (F - 32) * 5 / 9
40 PRINT F; " F = "; C; " C"
50 INPUT "Another (Y/N)"; A$
60 IF A$ = "Y" THEN 20
70 END
```

### Multiplication Table
```basic
10 PRINT "Multiplication Table"
20 PRINT
30 FOR I = 1 TO 10
40 FOR J = 1 TO 10
50 PRINT TAB(J*4); I*J;
60 NEXT J
70 PRINT
80 NEXT I
```

### Sorting
```basic
10 DIM A(20)
20 INPUT "How many numbers"; N
30 FOR I = 1 TO N
40 INPUT "Number"; A(I)
50 NEXT I
60 REM Bubble sort
70 FOR I = 1 TO N-1
80 FOR J = 1 TO N-I
90 IF A(J) > A(J+1) THEN T=A(J): A(J)=A(J+1): A(J+1)=T
100 NEXT J
110 NEXT I
120 PRINT "Sorted:"
130 FOR I = 1 TO N
140 PRINT A(I);
150 NEXT I
160 PRINT
```

### Fibonacci
```basic
10 INPUT "How many numbers"; N
20 A = 0: B = 1
30 FOR I = 1 TO N
40 PRINT A;
50 C = A + B
60 A = B: B = C
70 NEXT I
80 PRINT
```

---

## Multiple Statements

Use `:` to put multiple statements on one line:

```basic
10 A = 1: B = 2: C = A + B: PRINT C
```

---

## Error Messages

| Error | Description |
|-------|-------------|
| `SYNTAX ERROR` | Invalid syntax |
| `UNDEFINED LINE n` | GOTO/GOSUB to non-existent line |
| `RETURN WITHOUT GOSUB` | RETURN without matching GOSUB |
| `NEXT WITHOUT FOR` | NEXT without matching FOR |
| `DIVISION BY ZERO` | Division by zero |
| `SUBSCRIPT OUT OF RANGE` | Array index out of bounds |
| `OUT OF DATA` | READ with no more DATA |
| `UNDEFINED FUNCTION` | Unknown function name |
