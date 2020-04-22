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
//   $0000-$9FFF RAM (upper limit may vary based on MCU SRAM available)
//   $A000-$BFFF BASIC ROM
//   $A000-$BFFF Banked LORAM (may not be present based on MCU SRAM limits)
//   $C000-$CFFF RAM
//   $D000-$D7FF (I/O missing, reads as zeros)
//   $D800-$DFFF VIC-II color RAM nybbles in I/O space (1K x 4bits)
//   $D000-$DFFF Banked RAM (may not be present based on MCU SRAM limits)
//   $D000-$DFFF Banked Character ROM
//   $E000-$FFFF KERNAL ROM
//   $E000-$FFFF Banked HIRAM (may not be present based on MCU SRAM limits)
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
    public class EmuC64 : Emu6502
    {
        readonly ConsoleColor startup_fg = Console.ForegroundColor;
        readonly ConsoleColor startup_bg = Console.BackgroundColor;

        public EmuC64(int ram_size, string basic_file, string chargen_file, string kernal_file) : base(new C64Memory(ram_size, basic_file, chargen_file, kernal_file))
        {
            CBM_Console.ApplyColor = ApplyColor;
        }

        public string StartupPRG
        {
            get;
            set;
        }

        string FileName = null;
        byte FileNum = 0;
        byte FileDev = 0;
        byte FileSec = 0;
        bool FileVerify = false;
        ushort FileAddr = 0;

        private void ApplyColor()
        {
            bool reverse = (memory[199] != 0);
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

        int startup_state = 0;
        int LOAD_TRAP = -1;

        protected override bool ExecutePatch()
        {
            if (PC == 0xFFD2) // CHROUT
            {
                CBM_Console.WriteChar((char)A);
                // fall through to draw character in screen memory too
            }
            else if (PC == 0xFFCF) // CHRIN
            {
                A = CBM_Console.ReadChar();

                // SetA equivalent for flags
                Z = (A == 0);
                N = ((A & 0x80) != 0);
                C = false;

                // RTS equivalent
                byte lo = Pop();
                byte hi = Pop();
                PC = (ushort)(((hi << 8) | lo) + 1);

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            else if (PC == 0xA474 || PC == LOAD_TRAP) // READY
            {
                if (StartupPRG != null) // User requested program be loaded at startup
                {
                    bool is_basic;
                    if (PC == LOAD_TRAP)
                    {
                        is_basic = (
                            FileVerify == false
                            && FileSec == 0 // relative load, not absolute
                            && LO(FileAddr) == memory[43] // requested load address matches BASIC start
                            && HI(FileAddr) == memory[44]);
                        byte err;
                        if (!FileLoad(out err))
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("FileLoad() failed: err={0}, file {1}", err, StartupPRG));
                            C = true; // signal error
                            A = err; // FILE NOT FOUND or VERIFY

                            // so doesn't repeat
                            StartupPRG = null;
                            LOAD_TRAP = -1;

                            return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
                        }
                    }
                    else
                        is_basic = ((C64Memory)memory).LoadStartupPrg(StartupPRG, out FileAddr);

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

                        // JSR equivalent
                        ushort retaddr = (ushort)(PC - 1);
                        Push(HI(retaddr));
                        Push(LO(retaddr));
                        PC = 0xA533; // LINKPRG

                        startup_state = 1; // should be able to regain control when returns...

                        return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
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

                    // JSR equivalent
                    ushort retaddr = (ushort)(PC - 1);
                    Push(HI(retaddr));
                    Push(LO(retaddr));
                    PC = 0xA65E; // CLEAR/CLR
                    A = 0;

                    startup_state = 2; // should be able to regain control when returns...

                    return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
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
            else if (PC == 0xF13E) // GETIN
            {
                //BASIC TEST:
                //10 GET K$ : REM GETIN
                //20 IF K$<> "" THEN PRINT ASC(K$)
                //25 IF K$= "Q" THEN END
                //30 GOTO 10

                A = CBM_Console.GetIn();
                if (A == 0)
                {
                    Z = true;
                    C = false;
                }
                else
                {
                    X = A;
                    Z = false;
                    C = false;
                }

                // RTS equivalent
                byte lo = Pop();
                byte hi = Pop();
                PC = (ushort)(((hi << 8) | lo) + 1);

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            else if (PC == 0xF6ED) // STOP
            {
                Z = CBM_Console.CheckStop();

                // RTS equivalent
                byte lo = Pop();
                byte hi = Pop();
                PC = (ushort)(((hi << 8) | lo) + 1);

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            else if (PC == 0xFE00) // SETLFS
            {
                FileNum = A;
                FileDev = X;
                FileSec = Y;
                System.Diagnostics.Debug.WriteLine(string.Format("SETLFS {0},{1},{2}", FileNum, FileDev, FileSec));
            }
            else if (PC == 0xFDF9) // SETNAM
            {
                StringBuilder name = new StringBuilder();
                ushort addr = (ushort)(X + (Y << 8));
                for (int i = 0; i < A; ++i)
                    name.Append((char)memory[(ushort)(addr + i)]);
                System.Diagnostics.Debug.WriteLine(string.Format("SETNAM {0}", name.ToString()));
                FileName = name.ToString();
            }
            else if (PC == 0xF49E) // LOAD
            {
                FileAddr = (ushort)(X + (Y << 8));
                string op;
                if (A == 0)
                    op = "LOAD";
                else if (A == 1)
                    op = "VERIFY";
                else
                    op = string.Format("LOAD (A={0}) ???", A);
                FileVerify = (A == 1);
                System.Diagnostics.Debug.WriteLine(string.Format("{0} @{1:X4}", op, FileAddr));

                // RTS equivalent
                byte lo = Pop();
                byte hi = Pop();
                PC = (ushort)(((hi << 8) | lo) + 1);

                if (A == 0 || A == 1)
                {
                    StartupPRG = FileName;
                    FileName = null;
                    LOAD_TRAP = PC;

                    // Set success
                    C = false;
                }
                else
                {
                    A = 14; // ILLEGAL QUANTITY message
                    C = true; // failure
                }

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            else if (PC == 0xF5DD) // SAVE
            {
                ushort addr1 = (ushort)(memory[A] + (memory[(ushort)(A + 1)] << 8));
                ushort addr2 = (ushort)(X + (Y << 8));
                System.Diagnostics.Debug.WriteLine(string.Format("SAVE {0:X4}-{1:X4}", addr1, addr2));

                // RTS equivalent
                byte lo = Pop();
                byte hi = Pop();
                PC = (ushort)(((hi << 8) | lo) + 1);

                // Set success
                C = !FileSave(FileName, addr1, addr2);

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            return false; // execute normally
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

        static byte LO(ushort value)
        {
            return (byte)value;
        }

        static byte HI(ushort value)
        {
            return (byte)(value >> 8);
        }

        bool FileLoad(out byte err)
        {
            err = 0;
            ushort addr = FileAddr;
            bool success = true;
            try
            {
                string filename = StartupPRG;
                if (!File.Exists(filename) && !filename.ToLower().EndsWith(".prg"))
                    filename += ".prg";
                using (FileStream stream = File.OpenRead(filename))
                {
                    byte lo = (byte)stream.ReadByte();
                    byte hi = (byte)stream.ReadByte();
                    if (FileSec == 1) // use address in file? yes-use, no-ignore
                        addr = (ushort)(lo | (hi << 8)); // use address specified in file
                    int i;
                    while (success)
                    {
                        i = stream.ReadByte();
                        if (i >= 0 && i <= 255)
                        {
                            if (FileVerify)
                            {
                                if (memory[addr++] != (byte)i)
                                {
                                    err = 28; // VERIFY
                                    success = false;
                                }
                            }
                            else
                                memory[addr++] = (byte)i;
                        }
                        else
                            break; // end of file
                    }
                    stream.Close();
                }
            }
            catch (FileNotFoundException)
            {
                err = 4; // FILE NOT FOUND
                success = false;
            }
            catch (Exception)
            {
                err = 1; // UNKNOWN - TOO MANY FILES
                success = false;
            }
            FileAddr = addr;
            return success;
        }

        bool FileSave(string filename, ushort addr1, ushort addr2)
        {
            try
            {
                if (!filename.ToLower().EndsWith(".prg"))
                    filename += ".prg";
                using (FileStream stream = File.OpenWrite(filename))
                {
                    stream.WriteByte(LO(addr1));
                    stream.WriteByte(HI(addr1));
                    for (ushort addr = addr1; addr <= addr2; ++addr)
                        stream.WriteByte(memory[addr]);
                    stream.Close();
                    return true;
                }
            }
            catch
            {
                return false;
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
                    }
                    else if (addr >= color_addr && addr < color_addr + color_size)
                        io[addr - io_addr] = value;
                    //else if (addr >= io_addr && addr < io_addr + io.Length)
                    //    io[addr - io_addr] = value;
                }
            }

            // returns true if BASIC
            internal bool LoadStartupPrg(string filename, out ushort end_addr)
            {
                if (!File.Exists(filename) && !filename.ToLower().EndsWith(".prg"))
                    filename += ".prg";
                bool result;
                byte[] prg = File.ReadAllBytes(filename);
                ushort loadaddr;
                if (prg[0] == 1)
                {
                    loadaddr = (ushort)(ram[43] | (ram[44] << 8));
                    result = true;
                }
                else
                {
                    loadaddr = (ushort)(prg[0] | (prg[1] << 8));
                    result = false;
                }
                Array.Copy(prg, 2, ram, loadaddr, prg.Length - 2);
                end_addr = (ushort)(loadaddr + prg.Length - 2);
                return result;
            }
        }
    }
}
