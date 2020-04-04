// emuted.cs - Class EmuTED - Commodore TED Emulator (C16, Plus/4, etc.)
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
// This is a 6502 Emulator, designed for running Commodore computers in text mode, 
//   with only a few hooks: CHRIN-$FFCF/CHROUT-$FFD2, and minimal I/O to function
// Useful as is in current state as a simple 6502 emulator and BASIC console
//
// LIMITATIONS: See EmuC64
//
// MEMORY MAP:
//   $0000-$3FFF 16K RAM, note repeats $4000-$7FFF, $8000-$BFFF, $C000-$FFFF
//   ($0000-$7FFF 32K RAM, note repeats $8000-$FFFF)
//   ($0000-$FFFF 64K RAM)
//   $8000-$BFFF BASIC ROM
//   $FC00-$FCFF NON-BANKED KERNAL ROM (but can be banked to RAM)
//   $FD00-$FF3F I/O Registers (never banked, not even to RAM)
//   $FDD0-$FDDF ROM config addresses (A1/A0: BASIC/FUNCT/CART/RESV, A3/A2: KERNAL/FUNCT/CART/RESV)
//   $FF3E bank to ROM
//   $FF3F bank to RAM
//   $C000-$FFFF KERNAL ROM
//
// Requires user provided Commodore 16 BASIC/KERNAL ROMs (e.g. from VICE)
//   as they are not provided, others copyrights may still be in effect.
//
////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace simple_emu_c64
{
    public class EmuTED : Emu6502
    {
        public EmuTED(int ram_size, string basic_file, string kernal_file) : base(new TEDMemory(ram_size, basic_file, kernal_file))
        {
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

        class TEDMemory : Emu6502.Memory
        {
            byte[] ram; // note if less than 64K, then addressing wraps around
            byte[] basic_rom;
            byte[] kernal_rom;
            byte[] io;

            bool rom_enabled = true; // FF3E=rom & FF3F=ram

            // note ram starts at 0x0000
            const int basic_addr = 0x8000;
            const int kernal_addr = 0xC000;
            const int io_addr = 0xFD00; // IO cannot be banked ever
            const int io_length = 0x0240; // note hole in IO: FF20-FF3D, RAM or ROM?
            const int nonbank_kernal = 0xFC00; // can be banked to RAM, but not to other ROMs
            const int nonbank_len = 0x0100;
            const int config_addr = 0xFDD0;
            const int config_len = 0x0010;
            int rom_config = 0;

            // ROM configuration in I/O
            // A1/A0: 00=BASIC LO, 01=FUNCTION LO, 10=CARTRIDGE LO, 11=RESERVED LO
            // A3/A2: 00=KERNAL HI, 01=FUNCTION HI, 10=CARTRIDGE HI, 11=RESERVED HI
            // FDD0 BASIC LO/KERNAL HI
            // FDD1 FUNCTION LO/KERNAL HI
            // FDD2 CARTRIDGE LO/KERNAL HI
            // FDD3 RESERVED LO/KERNAL HI
            // FDD4 BASIC LO/FUNCTION HI
            // FDD5 FUNCTION LO/FUNCTION HI
            // FDD6 CARTRIDGE LO/FUNCTION HI
            // FDD7 RESERVED LO/FUNCTION HI
            // FDD8 BASIC LO/CARTRIDGE HI
            // FDD9 FUNCTION LO/CARTRIDGE HI
            // FDDA CARTRIDGE LO/CARTRIDGE HI
            // FDDB RESERVED/CARTRIDGE HI
            // FDDC BASIC LO/RESERVED HI
            // FDDD FUNCTION LO/RESERVED HI
            // FDDE CARTRIDGE LO/RESERVED HI
            // FDDF RESERVED LO/RESERVED HI

            public TEDMemory(int ram_size, string basic_file, string kernal_file)
            {
                if (ram_size != 16 * 1024 && ram_size != 32 * 1024 && ram_size != 64 * 1024)
                    throw new InvalidOperationException("Invalid RAM Length");

                ram = new byte[ram_size];
                basic_rom = File.ReadAllBytes(basic_file);
                kernal_rom = File.ReadAllBytes(kernal_file);

                for (int i = 0; i < ram.Length; ++i)
                    ram[i] = 0;

                io = new byte[io_length];
                for (int i = 0; i < io.Length; ++i)
                    io[i] = 0;
            }

            public byte this[ushort addr]
            {
                get
                {
                    if (addr == 0xFF08) // key matrix
                        return 0xFF; // FF = no key, 7F = stop (open in monitor)
                    if (addr >= io_addr && addr < io_addr + io.Length)
                        return io[addr - io_addr];
                    else if (!rom_enabled || addr < basic_addr)
                        return ram[addr & (ram.Length - 1)]; // note RAM wraps around when less than 64K
                    else if (((rom_config & 0x03) == 0) && rom_enabled && addr >= basic_addr && addr < basic_addr + basic_rom.Length)
                        return basic_rom[addr - basic_addr];
                    else if (((rom_config & 0x0C) == 0) && rom_enabled && (addr >= kernal_addr && addr < kernal_addr + kernal_rom.Length) || (addr >= nonbank_kernal && addr < nonbank_kernal + nonbank_len))
                        return kernal_rom[addr - kernal_addr];
                    else
                        return 0xFF;
                }

                set
                {
                    if (addr == 0xFF3E)
                        rom_enabled = true;
                    else if (addr == 0xFF3F)
                        rom_enabled = false;
                    else if (addr >= config_addr && addr < config_addr + config_len)
                        rom_config = addr & 0xF;
                    else if (addr >= io_addr && addr < io_addr + io.Length)
                        io[addr - io_addr] = value;
                    else
                        ram[addr & (ram.Length - 1)] = value; // includes writing under rom, note RAM wraps around when less than 64K
                }
            }
        }
    }
}
