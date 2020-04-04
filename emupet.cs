// emupet.cs - Class EmuPET - Commodore PET Emulator
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
// LIMITATIONS: See EmuC64
//
// PET 2001 MEMORY MAP:
//   $0000-$1FFF RAM (8k)
//   $8000-$8FFF Video RAM
//   $C000-$DFFF BASIC ROM (8K)
//   $E000-$E7FF Editor ROM (2K)
//   $E800-$EFFF I/O
//   $F000-$FFFF KERNAL ROM (4K)
//
// Requires user provided Commodore PET BASIC/KERNAL ROMs (e.g. from VICE)
//   as they are not provided, others copyrights may still be in effect.
//
////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace simple_emu_c64
{
    public class EmuPET : Emu6502
    {
        public EmuPET(string basic_file, string edit_file, string kernal_file) : base(new PETMemory(16*1024, basic_file, edit_file, kernal_file))
        {
            trace = true; // trace in effect, to track down problem starting in monitor
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

        class PETMemory : Emu6502.Memory
        {
            byte[] ram;
            byte[] video_ram;
            byte[] basic_rom;
            byte[] edit_rom;
            byte[] kernal_rom;
            byte[] io;

            const int video_addr = 0x8000;
            const int video_size = 0x1000;
            const int basic_addr = 0xC000;
            const int edit_addr = 0xE000;
            const int io_addr = 0xE800;
            const int io_size = 0x0800;
            const int kernal_addr = 0xF000;

            public PETMemory(int ram_size, string basic_file, string edit_file, string kernal_file)
            {
                ram = new byte[ram_size];
                io = new byte[io_size];
                video_ram = new byte[video_size];
                basic_rom = File.ReadAllBytes(basic_file);
                edit_rom = File.ReadAllBytes(edit_file);
                kernal_rom = File.ReadAllBytes(kernal_file);

                for (int i = 0; i < ram.Length; ++i)
                    ram[i] = 0;

                for (int i = 0; i < video_ram.Length; ++i)
                    video_ram[i] = 0;

                for (int i = 0; i < io.Length; ++i)
                    io[i] = 0;
            }

            public byte this[ushort addr]
            {
                get
                {
                    if (addr < ram.Length)
                        return ram[addr];
                    else if (addr >= video_addr && addr < video_addr + video_size)
                        return video_ram[addr - video_addr];
                    else if (addr >= basic_addr && addr < basic_addr + basic_rom.Length)
                        return basic_rom[addr - basic_addr];
                    else if (addr >= edit_addr && addr < edit_addr + edit_rom.Length)
                        return edit_rom[addr - edit_addr];
                    else if (addr >= io_addr && addr < io_addr + io.Length)
                        return io[addr - io_addr];
                    else if (addr >= kernal_addr && addr < kernal_addr + kernal_rom.Length)
                        return kernal_rom[addr - kernal_addr];
                    else
                        return 0xFF;
                }

                set
                {
                    if (addr < ram.Length)
                        ram[addr] = value;
                    else if (addr >= video_addr && addr < video_addr + video_size)
                        video_ram[addr - video_addr] = value;
                    else if (addr >= io_addr && addr < io_addr + io_size)
                        io[addr - io_addr] = value;
                }

            }
        }
    }
}
