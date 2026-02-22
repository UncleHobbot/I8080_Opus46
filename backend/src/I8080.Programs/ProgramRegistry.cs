using I8080.CpmSystem;

namespace I8080.Programs;

/// <summary>
/// Registers all built-in programs (ED, ASM, MBASIC) with the CP/M machine.
/// Also installs sample files on the virtual disk.
/// </summary>
public static class ProgramRegistry
{
    public static void RegisterAll(CpmMachine machine)
    {
        machine.RegisterProgram("ED", args =>
        {
            var editor = new EditorProgram(machine.Terminal, machine.Disk);
            editor.Run(args);
        });

        machine.RegisterProgram("ASM", args =>
        {
            var assembler = new AssemblerProgram(machine.Terminal, machine.Disk);
            assembler.Run(args);
        });

        machine.RegisterProgram("MBASIC", args =>
        {
            var basic = new BasicInterpreter(machine.Terminal, machine.Disk);
            basic.Run(args);
        });

        machine.RegisterProgram("BASIC", args =>
        {
            var basic = new BasicInterpreter(machine.Terminal, machine.Disk);
            basic.Run(args);
        });

        machine.RegisterProgram("HELP", _ =>
        {
            machine.Terminal.WriteLine("CP/M 2.2 Emulator - Available Commands");
            machine.Terminal.WriteLine("======================================");
            machine.Terminal.WriteLine();
            machine.Terminal.WriteLine("Built-in CCP commands:");
            machine.Terminal.WriteLine("  DIR [pattern]     - List files");
            machine.Terminal.WriteLine("  TYPE filename     - Display file contents");
            machine.Terminal.WriteLine("  ERA filename      - Erase file");
            machine.Terminal.WriteLine("  REN new=old       - Rename file");
            machine.Terminal.WriteLine("  USER n            - Set user number");
            machine.Terminal.WriteLine("  EXIT              - Exit emulator");
            machine.Terminal.WriteLine();
            machine.Terminal.WriteLine("Transient programs:");
            machine.Terminal.WriteLine("  ED filename       - Text editor");
            machine.Terminal.WriteLine("  ASM filename      - 8080 assembler");
            machine.Terminal.WriteLine("  MBASIC [filename] - BASIC interpreter");
            machine.Terminal.WriteLine("  HELP              - This help");
            machine.Terminal.WriteLine();
            machine.Terminal.WriteLine("You can also run .COM files from disk.");
        });

        InstallSampleFiles(machine.Disk);
    }

    private static void InstallSampleFiles(VirtualDisk disk)
    {
        // Sample BASIC program
        disk.WriteFile("HELLO.BAS",
            """
            10 REM Hello World in BASIC
            20 PRINT "Hello, World!"
            30 PRINT
            40 PRINT "Welcome to CP/M BASIC!"
            50 END
            """);

        disk.WriteFile("GUESS.BAS",
            """
            10 REM Number guessing game
            20 PRINT "I'm thinking of a number between 1 and 100."
            30 N = INT(RND(100)) + 1
            40 T = 0
            50 T = T + 1
            60 INPUT "Your guess";G
            70 IF G < N THEN PRINT "Too low!": GOTO 50
            80 IF G > N THEN PRINT "Too high!": GOTO 50
            90 PRINT "Correct! You got it in ";T;" tries!"
            100 END
            """);

        disk.WriteFile("FIB.BAS",
            """
            10 REM Fibonacci sequence
            20 INPUT "How many numbers";N
            30 A = 0: B = 1
            40 FOR I = 1 TO N
            50 PRINT A;
            60 C = A + B
            70 A = B: B = C
            80 NEXT I
            90 PRINT
            100 END
            """);

        // Sample assembly program
        disk.WriteFile("HELLO.ASM",
            """
            ; Hello World for CP/M
            ; Prints a message using BDOS function 9
            ;
                    ORG     0100H
            ;
            BDOS    EQU     0005H       ; BDOS entry point
            ;
            START:  MVI     C,9         ; BDOS function 9: print string
                    LXI     D,MSG       ; DE = address of message
                    CALL    BDOS        ; Call BDOS
                    RET                 ; Return to CCP
            ;
            MSG:    DB      'Hello from 8080 Assembly!',0DH,0AH,'$'
            ;
                    END
            """);

        disk.WriteFile("COUNT.ASM",
            """
            ; Count from 1 to 10
            ; Demonstrates loops and BDOS output
            ;
                    ORG     0100H
            ;
            BDOS    EQU     0005H
            ;
            START:  MVI     B,10        ; Counter
                    MVI     A,'1'       ; Start character
            ;
            LOOP:   PUSH    B           ; Save counter
                    PUSH    PSW         ; Save character
                    MOV     E,A         ; E = character to print
                    MVI     C,2         ; BDOS function 2: console output
                    CALL    BDOS
                    MVI     E,' '       ; Print space
                    MVI     C,2
                    CALL    BDOS
                    POP     PSW         ; Restore character
                    POP     B           ; Restore counter
                    INR     A           ; Next character
                    DCR     B           ; Decrement counter
                    JNZ     LOOP        ; Loop if not zero
            ;
                    MVI     E,0DH       ; Carriage return
                    MVI     C,2
                    CALL    BDOS
                    MVI     E,0AH       ; Line feed
                    MVI     C,2
                    CALL    BDOS
            ;
                    RET                 ; Return to CCP
            ;
                    END
            """);

        // Sample text file
        disk.WriteFile("README.TXT",
            """
            CP/M 2.2 Emulator
            =================

            This is an emulated CP/M system running on an
            Intel 8080 processor.

            Available programs:
            - ED: Text editor
            - ASM: 8080 assembler
            - MBASIC: BASIC interpreter
            - HELP: System help

            Type HELP for more information.
            Type DIR to see available files.
            """);
    }
}
