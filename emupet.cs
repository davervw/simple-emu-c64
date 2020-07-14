// emupet.cs - Class EmuPET - Commodore PET Emulator
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
// Useful as is in current state as a simple 6502 emulator
//
// LIMITATIONS: See EmuC64
//
// PET 2001 (chicklet keyboard) MEMORY MAP:
//   $0000-$1FFF RAM (8k)
//   $0000-$3FFF RAM (16k)
//   $0000-$7FFF RAM (32k)
//   $8000-$8FFF Video RAM
//   $C000-$DFFF BASIC ROM (8K)
//   $E000-$E7FF Editor ROM (2K)
//   $E800-$EFFF I/O
//   $F000-$FFFF KERNAL ROM (4K)
//
// Requires user provided Commodore PET BASIC/KERNAL ROMs (e.g. from VICE)
//   as they are not provided, others copyrights may still be in effect.
//
// References (and cool links):
//   https://archive.org/details/COMPUTEs_Programming_the_PET-CBM_1982_Small_Systems_Services
//   http://www.6502.org/users/andre/petindex/roms.html
//   http://www.6502.org/users/andre/petindex/progmod.html
//   https://en.wikipedia.org/wiki/Commodore_BASIC
//   http://www.weihenstephan.org/~michaste/pagetable/wait6502/msbasic_timeline.pdf
//   https://archive.org/details/COMPUTEs_First_Book_of_PET-CBM_1981_Small_Systems_Services
//   
////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace simple_emu_c64
{
    public class EmuPET : EmuCBM
    {
        public EmuPET(int ram_size, string basic_file, string edit_file, string kernal_file) : base(new PETMemory(ram_size, basic_file, edit_file, kernal_file))
        {
        }

        int startup_state = 0;

        // The patches implemented below are for basic1.  WARNING: basic2 is very different including zero page memory usage (e.g. $28/$29 instead of $7A/$7B)
        //    = INDEX, temporary BASIC pointer, set before CLR
        //   $7A/7B = start of BASIC program in RAM
        //   $7C/7D = end of BASIC program in RAM, start of variables
        //   $C38B = ROM BASIC READY prompt
        //   $C394 = ROM BASIC MAIN, in direct mode but skip READY prompt
        //   $C433= ROM LNKPRG/LINKPRG
        //   $C770 = CLEAR/CLR - erase variables (token 9C)
        //   $C6EC = EXECUTE can catch G for GO (because GO is not a keyword in basic1 ROM)
        //   execute RESET vector captured to clear screen via CHR$(147)
        protected override bool ExecutePatch()
        {
            if (PC == (ushort)(memory[0xFFFC] | (memory[0xFFFD] << 8)))
                CBM_Console.WriteChar((char)147, true); // PET 2001 doesn't initialize screen with chr$(147), so must do it here, supressing next home
            if (PC == 0xC38B || PC == LOAD_TRAP) // READY
            {
                //go_state = 0;

                if (StartupPRG != null) // User requested program be loaded at startup
                {
                    bool is_basic;
                    if (PC == LOAD_TRAP)
                    {
                        is_basic = (
                            FileVerify == false
                            && FileSec == 0 // relative load, not absolute
                        );
                        bool success = FileLoad(out byte err);
                        if (!success)
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
                        FileAddr = (ushort)(memory[0x7A] | (memory[0x7B] << 8));
                        is_basic = LoadStartupPrg();
                    }

                    StartupPRG = null;

                    if (is_basic)
                    {
                        // initialize first couple bytes (may only be necessary for UNNEW?)
                        ushort addr = (ushort)(memory[0x7A] | (memory[0x7B] << 8));
                        memory[addr] = 1;
                        memory[(ushort)(addr + 1)] = 1;

                        startup_state = 1; // should be able to regain control when returns...

                        return ExecuteJSR(0xC430); // Reset BASIC execution to start and fall through to LINKPRG - PET 2001 Basic 1.0
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
                    ushort addr = (ushort)(memory[0x71] | (memory[0x72] << 8) + 2);
                    memory[0x7C] = (byte)addr;
                    memory[0x7D] = (byte)(addr >> 8);

                    SetA(0);

                    startup_state = 2; // should be able to regain control when returns...

                    return ExecuteJSR(0xC56A); // CLEAR/CLR
                }
                else if (startup_state == 2)
                {
                    if (PC == LOAD_TRAP)
                    {
                        X = LO(FileAddr);
                        Y = HI(FileAddr);

                        PC = 0xC38B; // READY
                    }
                    else
                    {
                        CBM_Console.Push("RUN\r");
                        PC = 0xC394; // skip READY message, but still set direct mode, and continue to MAIN
                    }
                    C = false; // signal success
                    startup_state = 0;
                    LOAD_TRAP = -1;
                    return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
                }
            }
            else if (PC == 0xC6EC && A == 'G') // EXECUTE and found G where token expected
            {
                ushort addr = (ushort)(memory[0xC9] | (memory[0xCA] << 8));
                if (memory[(ushort)(++addr)] == 'O')
                {
                    // skip optional space
                    if (memory[(ushort)(++addr)] == ' ')
                        ++addr;

                    int digits = 0;
                    ushort num = 0;
                    while (true)
                    {
                        char c = (char)memory[addr++];
                        if (digits > 0 && (c == 0 || c == ':')) // end of number?
                        {
                            Program.go_num = num;
                            exit = true;
                            return true;
                        }
                        else if (c >= '0' && c <= '9')
                        {
                            ++digits;
                            num = Convert.ToUInt16(num * 10 + (c - '0')); // append digit, conversion from ASCII to ushort
                        }
                        else
                            return false;
                    }
                }
            }
            else if (PC == 0xF34E) // LOAD (after arguments parsed)
            {
                // PET is different, get arguments from low memory in fixed places
                FileAddr = (ushort)(memory[0xe5] | (memory[0xe6] << 8));
                FileVerify = (memory[0x20B] == 1);
                System.Text.StringBuilder filename = new System.Text.StringBuilder();
                int len = memory[0xEE];
                ushort fn_index = (ushort)(memory[0xF9] | (memory[0xFA] << 8));
                for (int i = 0; i < len; ++i)
                    filename.Append((char)memory[fn_index++]);
                StartupPRG = filename.ToString();
                FileSec = memory[0xF0];
                FileDev = memory[0xF1];

                string op;
                switch (memory[0x20B])
                {
                    case 0: op = "LOAD"; break;
                    case 1: op = "VERIFY"; break;
                    default: op = "???"; break;
                }
                System.Diagnostics.Debug.WriteLine(string.Format("{0} filename={1} device={2} sec={3} at={4:X4}", op, filename, FileDev, FileSec, FileAddr));

                if (FileSec == 0)
                    FileAddr = (ushort)(memory[0x7A] | (memory[0x7B] << 8)); // fix: PET erroneously sets address to $0100 instead of $0400 for relative

                System.Diagnostics.Debug.WriteLine(string.Format("{0} filename={1} device={2} sec={3} at={4:X4}", op, filename, FileDev, FileSec, FileAddr));

                ExecuteRTS();

                if (op != "???")
                    LOAD_TRAP = PC;

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            else if (PC == 0xFFD8) // SAVE
                ;
            else if ( // PET is different, so don't trap these addresses
                PC == 0xFFBA // SETLFS
                || PC == 0xFFBD // SETNAM
            )
                return false;

            // if got here, then call base class EmuCBM.ExecutePatch() for common code
            return base.ExecutePatch();
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
                if (ram_size > 32 * 1024) // too  big?
                    ram_size = 32 * 1024; // truncate

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
                    else if (addr == 0xE810) // PORT A
                        return 0xFF; // return FF otherwise hangs on start, key scan?
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
