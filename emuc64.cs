﻿// emuc64.cs - Class EmuC64 - Commodore 64 Emulator
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
//   and implemented RAM/ROM/IO banking
// Useful as is in current state as a simple 6502 emulator
//
// LIMITATIONS:
// Only keyboard/console I/O.  No text pokes, no graphics.  Just stdio.  
//   No asynchronous input (GET K$) or key scan codes, but INPUT S$ works
// No keyboard color switching.  No border displayed.  No border color.
// No screen editing (gasp!) Just short and sweet for running C64 BASIC in 
//   terminal/console window via 6502 chip emulation in software
// No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126), and CHR$(147) clear screen
// No timers.  No interrupts except BRK.  No NMI/RESTORE key.  No STOP key.
// No loading of files implemented.
//
//   $00/$01     (DDR and banking and I/O of 6510), RAM effectively hidden (haven't implemented techniques to access it)
//   $0000-$FFFF RAM (199=reverse if non-zero, 646=foreground color), note banking required for some address ranges, see $01
//   $A000-$BFFF BASIC ROM
//   $D000-$D7FF I/O
//   $D021       Background Screen Color
//   $D800-$DFFF VIC-II color RAM nybbles
//   $E000-$FFFF KERNAL ROM
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
            else if (PC == 0xA474) // READY
            {
                if (StartupPRG?.Length > 0) // User requested program be loaded at startup
                {
                    string filename = StartupPRG;
                    StartupPRG = null;

                    if (((C64Memory)memory).LoadPRG(filename))
                    {
                        StartupPRG = null;

                        //UNNEW that I used in late 1980s, should work well for loading a program too, probably gleaned from BASIC ROM
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

                        // This part shouldn't be necessary as we have loaded, not recovering from NEW, bytes should still be there
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
                    CBM_Console.Push("RUN\r");
                    PC = 0xA47B; // skip READY message, but still set direct mode, and continue to MAIN
                    C = false;
                    startup_state = 0;
                    return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
                }
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
            internal bool LoadPRG(string filename)
            {
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
                return result;
            }
        }
    }
}
