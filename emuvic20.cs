// emuvic20.cs - Class EmuVIC20 - Commodore VIC-20 Emulator
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
//   No asynchronous input (GET K$), but INPUT S$ works
// No keyboard color switching.  No border displayed.  No border color.
// No screen editing (gasp!) Just short and sweet for running C64 BASIC in 
//   terminal/console window via 6502 chip emulation in software
// No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126), plus CHR$(147) clear screen
// No memory management.  Not full 64K RAM despite startup screen.
//   4K to 39K RAM (28K max for BASIC), 16K ROM, 1K VIC-II color RAM nybbles
// No timers.  No interrupts except BRK.  No NMI/RESTORE key.  No STOP key.
// No loading of files implemented.
//
// MEMORY MAP:
//   $0000-$03FF Low 1K RAM (199=reverse if non-zero, 646=foreground color)
//   $0400-$0FFF (3K RAM expansion)
//   $1000-$1DFF 3K RAM (for BASIC)
//   $1E00-$1FFF RAM (Screen characters)
//   $2000-$7FFF (24K RAM expansion)
//   $8000-$9FFF (Character ROM)
//   $A000-$BFFF (8K Cartridge ROM, or RAM expansion)
//   $C000-$DFFF BASIC ROM
//   $E000-$FFFF KERNAL ROM
//
// Requires user provided Commodore Vic-20 BASIC/KERNAL ROMs (e.g. from VICE)
//   as they are not provided, others copyrights may still be in effect.
//
////////////////////////////////////////////////////////////////////////////////

//-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//uncomment for Commodore foreground, background colors and reverse emulation
//#define CBM_COLOR
//NOTE: Using C64 color mapping, not adjusted for VIC-20 differences yet
//-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

using System;
using System.IO;

namespace simple_emu_c64
{
    public class EmuVIC20 : Emu6502
    {
        public EmuVIC20(int ram_size, string char_file, string basic_file, string kernal_file) 
            : base(new VIC20Memory(RamSizeToRamConfig(ram_size), char_file, basic_file, kernal_file))
        {
#if CBM_COLOR
            CBM_Console.ApplyColor = ApplyColor;
#endif
            trace = false;
        }

        private static byte RamSizeToRamConfig(int ram_size)
        {
            if (ram_size == 4 * 1024)
                return 0x00;
            else if (ram_size == 7 * 1024)
                return 0x01;
            else if (ram_size == 12 * 1024)
                return 0x02;
            else if (ram_size == 15 * 1024)
                return 0x03;
            else if (ram_size == 20 * 1024)
                return 0x06;
            else if (ram_size == 23 * 1024)
                return 0x07;
            else if (ram_size == 28 * 1024)
                return 0x0E;
            else if (ram_size == 31 * 1024)
                return 0x0F;
            else if (ram_size == 36 * 1024)
                return 0x1E;
            else if (ram_size == 39 * 1024)
                return 0x1F;
            else
                return 0;
        }

        private void ApplyColor()
        {
            bool reverse = (memory[199] != 0) ^ ((memory[0x900F] & 0x8) == 0);
            if (reverse)
            {
                Console.BackgroundColor = ToConsoleColor(memory[646]);
                Console.ForegroundColor = ToConsoleColor(memory[0x900F]);
            }
            else
            {
                Console.ForegroundColor = ToConsoleColor(memory[646]);
                Console.BackgroundColor = ToConsoleColor(memory[0x900F]);
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

        static ConsoleColor ToConsoleColor(byte CommodoreColor)
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

        class VIC20Memory : Emu6502.Memory
        {
            byte[] ram;
            // ram_lo;         // 1K: 0000-03FF
            // ram_3k;         // 3K: 0400-0FFF (bank0, Optional)
            // ram_default;    // 4K: 1000-1FFF (Always present, default video is 1E00-1FFF)
            // ram_8k1;        // 8K: 2000-3FFF (bank1, Optional)
            // ram_8k2;        // 8K: 4000-5FFF (bank2, Optional)
            // ram_8k3;        // 8K: 6000-7FFF (bank3, Optional)
            byte[] char_rom;   // 4K: 8000-8FFF
            //byte[] io;         // 4K: 9000-9FFF (including video ram)
            // ram_rom         // 8K: A000-BFFF (bank4, Optional, Cartridge)
            byte[] basic_rom;  // 8K: C000-DFFF
            byte[] kernal_rom; // 8K: E000-FFFF

            byte ram_banks;

            const int ram3k_addr = 0x0400;
            const int ram4k_addr = 0x1000;
            const int ram8k1_addr = 0x2000;
            const int ram8k2_addr = 0x4000;
            const int ram8k3_addr = 0x6000;
            const int char_addr = 0x8000;
            const int io_addr = 0x9000;
            const int io_size = 0x1000;
            const int cart_addr = 0xA000;
            const int basic_addr = 0xC000;
            const int kernal_addr = 0xE000;

            public VIC20Memory(byte ram_banks, string char_file, string basic_file, string kernal_file)
            {
                this.ram_banks = ram_banks;

                // compute max ram address, allocate array based on that, even though all memory may not be present
                int ram_size = 0x2000;
                if ((ram_banks & 0x10) != 0)
                    ram_size = 0xC000;
                if ((ram_banks & 0x08) != 0 && ram_size < 0x8000)
                    ram_size = 0x8000;
                if ((ram_banks & 0x04) != 0 && ram_size < 0x6000)
                    ram_size = 0x6000;
                if ((ram_banks & 0x02) != 0 && ram_size < 0x4000)
                    ram_size = 0x4000;

                ram = new byte[ram_size];
                for (int i = 0; i < ram_size; ++i)
                    ram[i] = 0;

                //io = new byte[io_size];
                //for (int i = 0; i < io_size; ++i)
                //    io[i] = 0;

                char_rom = File.ReadAllBytes(char_file);
                basic_rom = File.ReadAllBytes(basic_file);
                kernal_rom = File.ReadAllBytes(kernal_file);
            }

            public byte this[ushort addr]
            {
                get
                {
                    if (addr < ram3k_addr)
                        return ram[addr];
                    else if (addr >= ram3k_addr && addr < ram4k_addr && ((ram_banks & 0x01) != 0))
                        return ram[addr];
                    else if (addr >= ram4k_addr && addr < ram8k1_addr)
                        return ram[addr];
                    else if (addr >= ram8k1_addr && addr < ram8k2_addr && ((ram_banks & 0x02) != 0))
                        return ram[addr];
                    else if (addr >= ram8k2_addr && addr < ram8k3_addr && ((ram_banks & 0x04) != 0))
                        return ram[addr];
                    else if (addr >= ram8k3_addr && addr < char_addr && ((ram_banks & 0x08) != 0))
                        return ram[addr];
                    else if (addr >= char_addr && addr < char_addr + char_rom.Length)
                        return char_rom[addr - char_addr];
                    else if (addr >= io_addr && addr < io_addr + io_size)
                        return 0; // io[addr - io_addr];
                    else if (addr >= cart_addr && addr < basic_addr && ((ram_banks & 0x10) != 0))
                        return ram[addr];
                    else if (addr >= basic_addr && addr < basic_addr + basic_rom.Length)
                        return basic_rom[addr - basic_addr];
                    else if (addr >= kernal_addr && addr < kernal_addr + kernal_rom.Length)
                        return kernal_rom[addr - kernal_addr];
                    else
                        return 0xFF;
                }

                set
                {
                    if (addr < ram3k_addr)
                        ram[addr] = value;
                    else if (addr >= ram3k_addr && addr < ram4k_addr && ((ram_banks & 0x01) != 0))
                        ram[addr] = value;
                    else if (addr >= ram4k_addr && addr < ram8k1_addr)
                        ram[addr] = value;
                    else if (addr >= ram8k1_addr && addr < ram8k2_addr && ((ram_banks & 0x02) != 0))
                        ram[addr] = value;
                    else if (addr >= ram8k2_addr && addr < ram8k3_addr && ((ram_banks & 0x04) != 0))
                        ram[addr] = value;
                    else if (addr >= ram8k3_addr && addr < char_addr && ((ram_banks & 0x08) != 0))
                        ram[addr] = value;
                    else if (addr >= io_addr && addr < io_addr + io_size)
                    {
                        //io[addr - io_addr] = value;
                        if (addr == 0x900F) // background/border/inverse
                        {
#if CBM_COLOR
                            bool reverse = (ram[199] != 0);

                            if (reverse)
                                Console.ForegroundColor = EmuVIC20.ToConsoleColor(value);
                            else
                                Console.BackgroundColor = EmuVIC20.ToConsoleColor(value);
#endif
                        }
                    }
                    else if (addr >= cart_addr && addr < basic_addr && ((ram_banks & 0x10) != 0))
                        ram[addr] = value;
                }
            }
        }
    }
}
