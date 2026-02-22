# ED - Text Editor Guide

## Overview

ED is a line-oriented text editor for CP/M. It can create and edit text files, assembly source files, and BASIC programs stored on disk.

## Starting ED

```
A>ED MYFILE.TXT
```

If the file exists, ED loads it and shows the line count. For new files:

```
A>ED NEWFILE.TXT
 : New file
*
```

The `*` is the ED command prompt.

---

## Commands

### Navigation

| Command | Description |
|---------|-------------|
| `n` | Go to line number n (e.g., `5` goes to line 5) |
| `G n` | Go to line n |
| `P [n]` | Page forward n lines (default 23) |
| `L [n]` | List n lines from current position |
| `N [n]` | Numbered list (same as L with line numbers) |
| `T [n]` | Type (display) line n |

### Editing

| Command | Description |
|---------|-------------|
| `I` | Insert mode - type lines, end with `.` on its own line |
| `I text` | Insert a single line of text |
| `A` | Append mode - add lines at end of file |
| `D [n]` | Delete n lines from current position |
| `K` | Kill (clear) entire buffer |

### Search and Replace

| Command | Description |
|---------|-------------|
| `S text` | Search forward for text |
| `R/old/new/` | Replace `old` with `new` in current line |
| `R/old/new/*` | Replace all occurrences in entire file |

Note: The delimiter after `R` can be any character (e.g., `R|old|new|`).

### File Operations

| Command | Description |
|---------|-------------|
| `W` | Write (save) file to disk |
| `E` | Save and exit |
| `Q` | Quit without saving |
| `H` | Display help |

---

## Examples

### Creating a New File

```
A>ED NOTES.TXT
 : New file
*I
1: This is line one.
2: This is line two.
3: Third line here.
4: .
*W
 : 3 lines written to NOTES.TXT
*Q
A>
```

### Editing an Existing File

```
A>ED NOTES.TXT
 : 3 lines read
*L
>    1: This is line one.
     2: This is line two.
     3: Third line here.
*2
2: This is line two.
*R/two/TWO/
2: This is line TWO.
*E
 : 3 lines written to NOTES.TXT
A>
```

### Search and Replace All

```
*R/foo/bar/*
5 replacements
```

### Inserting at a Specific Position

```
*G 2
2: This is line two.
*I New inserted line
*L
     1: This is line one.
>    2: New inserted line
     3: This is line two.
     4: Third line here.
```

---

## Tips

- The `>` marker in listings shows the current line
- Line numbers in ED are 1-based
- Use `L 1,999` to list the entire file (lines 1 through 999)
- Always save with `W` or `E` before exiting to avoid losing work
- `Q` will prompt for confirmation if there are unsaved changes
