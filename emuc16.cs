// emuc16.cs - Class EmuC16 - Commodore 16 Emulator
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
// MEMORY MAP
//   $0000-$3FFF RAM
//   $8000-$9FFF BASIC ROM
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
    public class EmuC16 : Emu6502
    {
        public EmuC16(string basic_file, string kernal_file) : base(new byte[65536])
        {
            byte[] basic_rom = File.ReadAllBytes(basic_file);
            byte[] kernal_rom = File.ReadAllBytes(kernal_file);

            for (int i = 0; i < memory.Length; ++i)
                memory[i] = 0;

            Array.Copy(basic_rom, 0, memory, 0x8000, basic_rom.Length);
            Array.Copy(kernal_rom, 0, memory, 0xC000, kernal_rom.Length);
        }

        protected override void SetMemory(ushort addr, byte value)
        {
            if (addr < 0x4000)
            {
                base.SetMemory(addr, value);
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
    }
}
