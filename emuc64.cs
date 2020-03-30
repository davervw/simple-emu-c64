// emuc64.cs - Class EmuC64 - Commodore 64 Emulator
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright (c) 2020 by David R. Van Wagner ALL RIGHTS RESERVED
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
// Useful as is in current state as a simple 6502 emulator
//
// LIMITATIONS:
// Only keyboard/console I/O.  No text pokes, no graphics.  Just stdio.  
//   No backspace.  No asynchronous input (GET K$), but INPUT S$ works
// No keyboard color switching.  No border or border color.
// No screen editing (gasp!) Just short and sweet for running C64 BASIC in 
//   terminal/console window via 6502 chip emulation in software
// No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126)
// No memory management.  Not full 64K RAM despite startup screen.
//   Just 44K RAM, 16K ROM, 1K VIC-II color RAM nybbles
// No timers.  No interrupts except BRK.  No NMI/RESTORE key.  No STOP key.
// No loading of files implemented.
//
//   $00/$01     (DDR and banking and I/O of 6510 missing), just RAM
//   $0000-$9FFF RAM (199=reverse if non-zero, 646=foreground color)
//   $A000-$BFFF BASIC ROM (no RAM underneath)
//   $C000-$CFFF RAM
//   $D000-$DFFF (missing I/O and character ROM and RAM banks), just zeros except...
//   $D021       Background Screen Color
//   $D800-$DFFF VIC-II color RAM nybbles
//   $E000-$FFFF KERNAL ROM (no RAM underneath)
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
        public EmuC64(string basic_file, string kernal_file) : base(new byte[65536])
        {
            byte[] basic_rom = File.ReadAllBytes(basic_file);
            byte[] kernal_rom = File.ReadAllBytes(kernal_file);

            for (int i = 0; i < memory.Length; ++i)
                memory[i] = 0;

            Array.Copy(basic_rom, 0, memory, 0xA000, basic_rom.Length);
            Array.Copy(kernal_rom, 0, memory, 0xE000, kernal_rom.Length);

#if CBM_COLOR
            CBM_Console.ApplyColor = ApplyColor;
#endif
        }

        protected override void SetMemory(ushort addr, byte value)
        {
            if (addr < 0xA000 || (addr >= 0xC000 && addr < 0xD000) || (addr >= 0xD800 && addr < 0xDC00)) // only allow writing to RAM (not ROM or I/O)
            {
                base.SetMemory(addr, value);
            }
            else if (addr == 0xD021) // background
            {
#if CBM_COLOR
                bool reverse = (memory[199] != 0);

                if (reverse)
                    Console.ForegroundColor = ToConsoleColor(value);
                else
                    Console.BackgroundColor = ToConsoleColor(value);
#endif

                base.SetMemory(addr, (byte)(value & 0xF)); // store value so can be retrieved
            }
        }

        private void ApplyColor()
        {
            bool reverse = (memory[199] != 0);
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
        }

        protected override bool ExecutePatch()
        {
            if (base.PC == 0xFFD2) // CHROUT
            {
                CBM_Console.WriteChar((char)A);
                // fall through to draw character in screen memory too
            }
            else if (base.PC == 0xFFCF) // CHRIN
            {
                A = CBM_Console.ReadChar();

                // SetA equivalent for flags
                Z = (A == 0);
                N = ((A & 0x80) != 0);
                C = false;

                // RTS equivalent
                byte lo = base.Pop();
                byte hi = base.Pop();
                base.PC = (ushort)(((hi << 8) | lo) + 1);

                return true; // overriden, so don't execute
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
            Array.Copy(memory, 0xE3A2, memory, 0x73, 0x18); // CHRGET

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

            // Portion of MAIN, CRUNCH and GONE(Execute) or MAIN1(Store line)
            Walk6502.Walk(this, 0xA494);

            ushort addr;

            for (ushort table = 0xA00C; table < 0xA051; table += 2) // BASIC Statements
            {
                addr = (ushort)((memory[table] | (memory[table + 1] << 8)) + 1); // put on stack for RTS, so must add one
                Walk6502.Walk(this, addr);
            }

            for (ushort table = 0xA052; table < 0xA07F; table += 2) // Function Dispatch
            {
                addr = (ushort)((memory[table] | (memory[table + 1] << 8)));
                Walk6502.Walk(this, addr);
            }

            for (ushort table = 0xA080; table < 0xA09D; table += 3) // Operator Dispatch
            {
                addr = (ushort)((memory[table + 1] | (memory[table + 2] << 8)) + 1); // put on stack for RTS, so must add one
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
    }
}
