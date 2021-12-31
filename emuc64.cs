// emuc64.cs - Class EmuC64 - Commodore 64 Emulator
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright (c) 2020 by David R. Van Wagner
// davevw.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
////////////////////////////////////////////////////////////////////////////////
//
// This is a 6502 Emulator, designed for running Commodore 64 text mode, 
//   with only a few hooks: CHRIN-$FFCF/CHROUT-$FFD2/COLOR-$D021/199/646
//   and implemented RAM/ROM/IO banking (BASIC could live without these)
//   additional hooks added: READY/GETIN/STOP (could live without these)
//   READY hook is used to load program specified on command line
// Useful as is in current state as a simple 6502 emulator
//
// LIMITATIONS:
// Only keyboard/console I/O.  No text pokes, no graphics.  Just stdio.  
//   No key scan codes (197), or keyboard buffer (198, 631-640), but INPUT S$ works
// No keyboard color switching.  No border displayed.  No border color.
// No screen editing (gasp!) Just short and sweet for running C64 BASIC in 
//   terminal/console window via 6502 chip emulation in software
// No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126), 
//   and CHR$(147) clear screen, home/up/down/left/right, reverse on/off
// No timers.  No interrupts except BRK.  No NMI/RESTORE key.  ESC is STOP key.
// No loading of files implemented.
//
//   $00         (data direction missing)
//   $01         Banking implemented (tape sense/controls missing)
//   $0000-$9FFF RAM (upper limit may vary based on RAM allocated)
//   $A000-$BFFF BASIC ROM
//   $A000-$BFFF Banked LORAM (may not be present based on RAM allocated)
//   $C000-$CFFF RAM
//   $D000-$D7FF (I/O missing, reads as zeros)
//   $D800-$DFFF VIC-II color RAM nybbles in I/O space (1K x 4bits)
//   $D000-$DFFF Banked RAM (may not be present based on RAM allocated)
//   $D000-$DFFF Banked Character ROM
//   $E000-$FFFF KERNAL ROM
//   $E000-$FFFF Banked HIRAM (may not be present based on RAM allocated)
//
// Requires user provided Commodore 64 BASIC/KERNAL ROMs (e.g. from VICE)
//   as they are not provided, others copyrights may still be in effect.
//
////////////////////////////////////////////////////////////////////////////////

//-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//uncomment for Commodore foreground, background colors and reverse emulation
//#define CBM_COLOR
//-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

using System;
using System.IO;
using System.Text;

namespace simple_emu_c64
{
    public class EmuC64 : EmuCBM
    {
        public EmuC64(int ram_size, string basic_file, string chargen_file, string kernal_file) : base(new C64Memory(ram_size, basic_file, chargen_file, kernal_file))
        {
            CBM_Console.ApplyColor = ApplyColor;
        }

        private static void ApplyColor(bool reverse)
        {
#if CBM_COLOR
            if (reverse)
            {
                Console.BackgroundColor = ToConsoleColor(memory[646]);
                Console.ForegroundColor = ToConsoleColor(memory[0xD021]);
            }
            else
            {
                Console.ForegroundColor = ToConsoleColor(memory[646]);
                Console.BackgroundColor = ToConsoleColor(memory[0xD021]);
            }
#else
            if (reverse)
            {
                Console.BackgroundColor = startup_fg;
                Console.ForegroundColor = startup_bg;
            }
            else
            {
                Console.ForegroundColor = startup_fg;
                Console.BackgroundColor = startup_bg;
            }
#endif
        }

        private static bool Reverse(Emu6502.Memory memory)
        {
            return (memory[199] != 0);
        }

        private void ApplyColor()
        {
            ApplyColor(Reverse(memory));
        }

        private int startup_state = 0;
        private int go_state = 0;

        // C64 patches
        //   34/35 ($22/$23) = INDEX, temporary BASIC pointer, set before CLR
        //   43/44 = start of BASIC program in RAM
        //   45/46 = end of BASIC program in RAM, start of variables
        //   $A474 = ROM BASIC READY prompt
        //   $A47B = ROM BASIC MAIN, in direct mode but skip READY prompt
        //   $A533 = ROM LNKPRG/LINKPRG
        //   $A65E = CLEAR/CLR - erase variables
        //   $A815 = EXECUTE after parsing GO token
        //   $AD8A = Evaluate expression, check data type
        //   $B7F7 = Convert floating point to 2 byte integer Y/A
        protected override bool ExecutePatch()
        {
            if (memory[PC] == 0x6C && memory[(ushort)(PC + 1)] == 0x30 && memory[(ushort)(PC + 2)] == 0x03) // catch JMP(LOAD_VECTOR), redirect to jump table
            {
                CheckBypassSETLFS();
                CheckBypassSETNAM();
                // note: A register has same purpose LOAD/VERIFY
                X = memory[0xC3];
                Y = memory[0xC4];
                PC = 0xFFD5; // use KERNAL JUMP TABLE instead, so LOAD is hooked by base
                return true; // re-execute
            }
            if (memory[PC] == 0x6C && memory[(ushort)(PC + 1)] == 0x32 && memory[(ushort)(PC + 2)] == 0x03) // catch JMP(SAVE_VECTOR), redirect to jump table
            {
                CheckBypassSETLFS();
                CheckBypassSETNAM();
                X = memory[0xAE];
                Y = memory[0xAF];
                A = 0xC1;
                PC = 0xFFD8; // use KERNAL JUMP TABLE instead, so SAVE is hooked by base
                return true; // re-execute
            }
            else if (PC == 0xA474 || PC == LOAD_TRAP) // READY
            {
                go_state = 0;

                if (startup_state == 0 && (StartupPRG != null || PC == LOAD_TRAP))
                {
                    bool is_basic;
                    if (PC == LOAD_TRAP)
                    {
                        is_basic = (
                            FileVerify == false
                            && FileSec == 0 // relative load, not absolute
                            && LO(FileAddr) == memory[43] // requested load address matches BASIC start
                            && HI(FileAddr) == memory[44]);
                        if (FileLoad(out byte err))
                        {
                            // set End of Program
                            memory[0xAE] = (byte)FileAddr;
                            memory[0xAF] = (byte)(FileAddr >> 8);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("FileLoad() failed: err={0}, file {1}", err, StartupPRG));
                            C = true; // signal error
                            SetA(err); // FILE NOT FOUND or VERIFY

                            // so doesn't repeat
                            StartupPRG = null;
                            LOAD_TRAP = -1;

                            return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
                        }
                    }
                    else
                    {
                        FileName = StartupPRG;
                        FileAddr = (ushort)(memory[43] | (memory[44] << 8));
                        is_basic = LoadStartupPrg();
                        // set End of Program
                        memory[0xAE] = (byte)FileAddr;
                        memory[0xAF] = (byte)(FileAddr >> 8);
                    }

                    StartupPRG = null;

                    if (is_basic)
                    {
                        //UNNEW that I used in late 1980s, should work well for loading a program too, probably gleaned from BASIC ROM
                        //listed here as reference, adapted to use in this state machine, ExecutePatch()
                        //ldy #0
                        //lda #1
                        //sta(43),y
                        //iny
                        //sta(43),y
                        //jsr $a533 ; LINKPRG
                        //clc
                        //lda $22
                        //adc #2
                        //sta 45
                        //lda $23
                        //adc #0
                        //sta 46
                        //lda #0
                        //jsr $a65e ; CLEAR/CLR
                        //jmp $a474 ; READY

                        // initialize first couple bytes (may only be necessary for UNNEW?)
                        ushort addr = (ushort)(memory[43] | (memory[44] << 8));
                        memory[addr] = 1;
                        memory[(ushort)(addr + 1)] = 1;

                        startup_state = 1; // should be able to regain control when returns...

                        return ExecuteJSR(0xA533); // LINKPRG
                    }
                    else
                    {
                        LOAD_TRAP = -1;
                        X = LO(FileAddr);
                        Y = HI(FileAddr);
                        C = false;
                    }
                }
                else if (startup_state == 1)
                {
                    ushort addr = (ushort)(memory[0x22] | (memory[0x23] << 8) + 2);
                    memory[45] = (byte)addr;
                    memory[46] = (byte)(addr >> 8);

                    SetA(0);

                    startup_state = 2; // should be able to regain control when returns...

                    return ExecuteJSR(0xA65E); // CLEAR/CLR
                }
                else if (startup_state == 2)
                {
                    if (PC == LOAD_TRAP)
                    {
                        X = LO(FileAddr);
                        Y = HI(FileAddr);
                    }
                    else
                    {
                        CBM_Console.Push("RUN\r");
                        PC = 0xA47B; // skip READY message, but still set direct mode, and continue to MAIN
                    }
                    C = false; // signal success
                    startup_state = 0;
                    LOAD_TRAP = -1;
                    return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
                }
            }
            else if (PC == 0xA815) // Execute after GO
            {
                if (go_state == 0 && A >= (byte)'0' && A <= (byte)'9')
                {
                    go_state = 1;
                    return ExecuteJSR(0xAD8A); // Evaluate expression, check data type
                }
                else if (go_state == 1)
                {
                    go_state = 2;
                    return ExecuteJSR(0xB7F7); // Convert fp to 2 byte integer
                }
                else if (go_state == 2)
                {
                    Program.go_num = (ushort)(Y + (A << 8));
                    exit = true;
                    return true;
                }
            }

            return base.ExecutePatch();
        }

        private void CheckBypassSETNAM()
        {
            // In case caller bypassed calling SETNAM, get from lower memory
            byte name_len = memory[0xB7];
            ushort name_addr = (ushort)(memory[0xBB] | (memory[0xBC] << 8));
            StringBuilder name = new StringBuilder();
            for (byte i = 0; i < name_len; ++i)
                name.Append((char)memory[(ushort)(name_addr + i)]);
            if (FileName != name.ToString())
            {
                System.Diagnostics.Debug.WriteLine(string.Format("bypassed SETNAM {0}", name.ToString()));
                FileName = name.ToString();
            }
        }

        private void CheckBypassSETLFS()
        {
            // In case caller bypassed calling SETLFS, get from lower memory
            if (
                FileNum != memory[0xB8]
                || FileDev != memory[0xBA]
                || FileSec != memory[0xB9]
            )
            {
                FileNum = memory[0xB8];
                FileDev = memory[0xBA];
                FileSec = memory[0xB9];
                System.Diagnostics.Debug.WriteLine(string.Format("bypassed SETLFS {0},{1},{2}", FileNum, FileDev, FileSec));
            }
        }

        ConsoleColor ToConsoleColor(byte CommodoreColor)
        {
            switch (CommodoreColor & 0xF)
            {
                case 0: return ConsoleColor.Black;
                case 1: return ConsoleColor.White;
                case 2: return ConsoleColor.Red;
                case 3: return ConsoleColor.Cyan;
                case 4: return ConsoleColor.DarkMagenta;
                case 5: return ConsoleColor.DarkGreen;
                case 6: return ConsoleColor.DarkBlue;
                case 7: return ConsoleColor.Yellow;
                case 8: return ConsoleColor.DarkYellow;
                case 9: return ConsoleColor.DarkRed;
                case 10: return ConsoleColor.Magenta;
                case 11: return ConsoleColor.DarkCyan;
                case 12: return ConsoleColor.DarkGray;
                case 13: return ConsoleColor.Green;
                case 14: return ConsoleColor.Blue;
                case 15: return ConsoleColor.Gray;
                default: throw new InvalidOperationException("Missing case number in ToConsoleColor");
            }
        }

        // Commodore 64 - walk Kernal Reset vector, MAIN, CRUNCH, GONE (EXECUTE), Statements, Functions, and Operators BASIC ROM code
        public override void Walk()
        {
            // in case cpu has not been reset, manually initialize low memory that will be called by BASIC and KERNAL ROM
            for (int i = 0; i < 0x18; ++i)
                memory[(ushort)(0x73 + i)] = memory[(ushort)(0xE3A2 + i)]; // CHARGET

            memory[0x300] = LO(0xE38B); // ERROR
            memory[0x301] = HI(0xE38B);

            memory[0x302] = LO(0xA483); // MAIN
            memory[0x303] = HI(0xA483);

            memory[0x304] = LO(0xA57C); // CRUNCH
            memory[0x305] = HI(0xA57C);

            memory[0x306] = LO(0xA71A); // QPLOP
            memory[0x307] = HI(0xA71A);

            memory[0x308] = LO(0xA7E4); // GONE
            memory[0x309] = HI(0xA7E4);

            memory[0x30A] = LO(0xAE86); // EVAL
            memory[0x30B] = HI(0xAE86);

            memory[0x31A] = LO(0xF34A); // OPEN
            memory[0x31B] = HI(0xF34A);

            memory[0x31C] = LO(0xF291); // CLOSE
            memory[0x31D] = HI(0xF291);

            memory[0x31E] = LO(0xF20E); // CHKIN
            memory[0x31F] = HI(0xF20E);

            memory[0x320] = LO(0xF250); // CKOUT
            memory[0x321] = HI(0xF250);

            memory[0x322] = LO(0xF333); // CHECK STOP
            memory[0x323] = HI(0xF333);

            memory[0x324] = LO(0xF157); // CHRIN
            memory[0x325] = HI(0xF157);

            memory[0x326] = LO(0xF1CA); // CHROUT
            memory[0x327] = HI(0xF1CA);

            memory[0x328] = LO(0xF6ED); // STOP
            memory[0x329] = HI(0xF6ED);

            memory[0x32A] = LO(0xF13E); // GETIN
            memory[0x32B] = HI(0xF13E);

            memory[0x32C] = LO(0xF32F); // CLALL
            memory[0x32D] = HI(0xF32F);

            memory[0x330] = LO(0xF4A5); // LOAD
            memory[0x331] = HI(0xF4A5);

            memory[0x332] = LO(0xF5ED); // SAVE
            memory[0x333] = HI(0xF5ED);

            base.Walk(); // reset seen, and walk the RESET vector

            ushort addr;

            addr = (ushort)(memory[0xFFFE] | (memory[0xFFFF] << 8)); // IRQ
            Walk6502.Walk(this, addr);

            addr = (ushort)(memory[0xFFFA] | (memory[0xFFFB] << 8)); // NMI
            Walk6502.Walk(this, addr);

            // Portion of MAIN, CRUNCH and GONE(Execute) or MAIN1(Store line)
            Walk6502.Walk(this, 0xA494);

            for (ushort table = 0xA00C; table < 0xA051; table += 2) // BASIC Statements
            {
                addr = (ushort)((memory[table] | (memory[(ushort)(table + 1)] << 8)) + 1); // put on stack for RTS, so must add one
                Walk6502.Walk(this, addr);
            }

            for (ushort table = 0xA052; table < 0xA07F; table += 2) // Function Dispatch
            {
                addr = (ushort)((memory[table] | (memory[(ushort)(table + 1)] << 8)));
                Walk6502.Walk(this, addr);
            }

            for (ushort table = 0xA080; table < 0xA09D; table += 3) // Operator Dispatch
            {
                addr = (ushort)((memory[(ushort)(table + 1)] | (memory[(ushort)(table + 2)] << 8)) + 1); // put on stack for RTS, so must add one
                Walk6502.Walk(this, addr);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        class C64Memory : Emu6502.Memory
        {
            byte[] ram;
            byte[] basic_rom;
            byte[] char_rom;
            byte[] kernal_rom;
            byte[] io;

            // note ram starts at 0x0000
            const int basic_addr = 0xA000;
            const int kernal_addr = 0xE000;
            const int io_addr = 0xD000;
            const int io_size = 0x1000;
            const int color_addr = 0xD800;
            const int color_size = 0x0400;
            const int open_addr = 0xC000;
            const int open_size = 0x1000;

            public C64Memory(int ram_size, string basic_file, string chargen_file, string kernal_file)
            {
                if (ram_size > 64 * 1024)
                    ram_size = 64 * 1024;
                ram = new byte[ram_size];
                basic_rom = File.ReadAllBytes(basic_file);
                char_rom = File.ReadAllBytes(chargen_file);
                kernal_rom = File.ReadAllBytes(kernal_file);

                for (int i = 0; i < ram.Length; ++i)
                    ram[i] = 0;

                io = new byte[io_size];
                for (int i = 0; i < io.Length; ++i)
                    io[i] = 0;

                // initialize DDR and memory mapping to defaults
                ram[0] = 0xEF;
                ram[1] = 0x07;
            }

            public byte this[ushort addr]
            {
                get
                {
                    if (addr <= ram.Length - 1 // note: handles option to have less than 64K RAM
                          && (
                            addr < basic_addr // always RAM
                            || (addr >= open_addr && addr < open_addr + open_size) // always open RAM C000.CFFF
                            || (((ram[1] & 3) != 3) && addr >= basic_addr && addr < basic_addr + basic_rom.Length) // RAM banked instead of BASIC
                            || (((ram[1] & 2) == 0) && addr >= kernal_addr && addr <= kernal_addr + kernal_rom.Length - 1) // RAM banked instead of KERNAL
                            || (((ram[1] & 3) == 0) && addr >= io_addr && addr < io_addr + io.Length) // RAM banked instead of IO
                          )
                        )
                        return ram[addr];
                    else if (addr >= basic_addr && addr < basic_addr + basic_rom.Length)
                        return basic_rom[addr - basic_addr];
                    else if (addr >= io_addr && addr < io_addr + io.Length)
                    {
                        if ((ram[1] & 4) == 0)
                            return char_rom[addr - io_addr];
                        else if (addr >= color_addr && addr < color_addr + color_size)
                            return (byte)(io[addr - io_addr] | 0xF0); // set high bits to show this is a nybble
                        else
                            return io[addr - io_addr];
                    }
                    else if (addr >= kernal_addr && addr <= kernal_addr + kernal_rom.Length - 1)
                        return kernal_rom[addr - kernal_addr];
                    else
                        return 0xFF;
                }

                set
                {
                    if (addr <= ram.Length - 1  // note: handles option to have less than 64K RAM
                          && (
                            addr < io_addr // RAM, including open RAM, and RAM under BASIC
                            || (addr >= kernal_addr && addr <= kernal_addr + kernal_rom.Length - 1) // RAM under KERNAL
                            || (((ram[1] & 7) == 0) && addr >= io_addr && addr < io_addr + io.Length) // RAM banked in instead of IO
                          )
                        )
                        ram[addr] = value; // banked RAM, and RAM under ROM
                    else if (addr == 0xD021) // background
                    {
#if CBM_COLOR
                        Console.BackgroundColor = ToConsoleColor(value);
#endif
                        io[addr - io_addr] = (byte)(value & 0xF); // store value so can be retrieved
                        ApplyColor(Reverse(this));
                    }
                    else if (addr >= color_addr && addr < color_addr + color_size)
                        io[addr - io_addr] = value;
                    //else if (addr >= io_addr && addr < io_addr + io.Length)
                    //    io[addr - io_addr] = value;
                }
            }
        }
    }
}
