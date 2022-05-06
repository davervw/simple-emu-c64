// emuc128.cs - Class EmuC128 - Commodore 128 Emulator
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright (c) 2020-2022 by David R. Van Wagner
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
// This is a 6502 Emulator, designed for running Commodore 128 text mode, 
//   with only a few hooks: CHRIN/CHROUT/COLOR-$D021/241/243/READY/GETIN/STOP
//   and RAM/ROM/IO banking from 6510, and C128 MMU
//   READY hook is used to load program specified on command line
//
// LIMITATIONS:
// Only keyboard/console I/O.  No text pokes, no graphics.  Just stdio.  
//   No key scan codes (197), or keyboard buffer (198, 631-640), but INPUT S$ works
// No keyboard color switching.  No border displayed.  No border color.
// No screen editing (gasp!) Just short and sweet for running C128 BASIC in 
//   terminal/console window via 8502 (6502) chip emulation in software
// No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126), 
//   and CHR$(147) clear screen, home/up/down/left/right, reverse on/off
// No timers.  No interrupts except BRK.  No NMI/RESTORE key.  ESC is STOP key.
//   but TI$/TI are simulated.
//
//   $00         On chip (8502) data direction register missing in this emulation
//   $01         On chip (8502) I/O register minimally implemented
//
//   $0002-$3FFF RAM (BANK 0 or BANK 1)
//
//   $4000-$7FFF BASIC ROM LO
//   $4000-$7FFF Banked RAM (BANK 0 or BANK 1)
//
//   $8000-$BFFF BASIC ROM HI
//   $8000-$BFFF Banked RAM (BANK 0 or BANK 1)
//
//   $C000-$FFFF Banked KERNAL/CHAR(DXXX) ROM
//   $C000-$FFFF Banked RAM (BANK 0 or BANK 1)
//
//   $D000-$D7FF I/O minimally implemented, reads as zeros
//   $D800-$DFFF VIC-II color RAM nybbles in I/O space (1K x 4bits)
//   $D000-$DFFF Banked Character ROM
//   $D000-$DFFF Banked RAM (BANK 0 or BANK 1)
//
// Requires user provided Commodore 128 BASIC/KERNAL ROMs (e.g. from VICE)
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
    public class EmuC128 : EmuCBM
    {
        public EmuC128(string basic_lo_file, string basic_hi_file, string chargen_file, string kernal_file) : base(new C128Memory(basic_lo_file, basic_hi_file, chargen_file, kernal_file))
        {
        }

        private int startup_state = 0;
        //private int go_state = 0;
        private bool esc_mode = false;

        protected override bool ExecutePatch()
        {
            if (PC == CHAROUT_TRAP)
            {
                if (A == 27)
                {
                    esc_mode = !esc_mode;
                    CHAROUT_TRAP = -1;
                    return true; // trap again, but not outputting to stdoutl
                }
                else if (esc_mode)
                {
                    esc_mode = false;
                    CHAROUT_TRAP = -1;
                    return true; // trap again, but not outputting to stdout
                }
            }
            if (memory[PC] == 0x6C && memory[(ushort)(PC + 1)] == 0x30 && memory[(ushort)(PC + 2)] == 0x03) // catch JMP(LOAD_VECTOR), redirect to jump table
            {
                int addr128k = 0x330;
                if (IsRam(ref addr128k) && addr128k == 0x330)
                {
                    CheckBypassSETLFS();
                    CheckBypassSETNAM();
                    // note: A register has same purpose LOAD/VERIFY
                    X = memory[0xC3];
                    Y = memory[0xC4];
                    PC = 0xFFD5; // use KERNAL JUMP TABLE instead, so LOAD is hooked by base
                    return true; // re-execute
                }
            }
            if (memory[PC] == 0x6C && memory[(ushort)(PC + 1)] == 0x32 && memory[(ushort)(PC + 2)] == 0x03) // catch JMP(SAVE_VECTOR), redirect to jump table
            {
                int addr128k = 0x332;
                if (IsRam(ref addr128k) && addr128k == 0x332)
                {
                    CheckBypassSETLFS();
                    CheckBypassSETNAM();
                    X = memory[0xAE];
                    Y = memory[0xAF];
                    A = 0xC1;
                    PC = 0xFFD8; // use KERNAL JUMP TABLE instead, so SAVE is hooked by base
                    return true; // re-execute
                }
            }

            // Note: BANK # (0-15) for file i/o is in $C6
            // Note: BANK # (0-15) for filename is in $C7
            // Note: BANK # to MMU CR translation table in Kernal at $F7F0
            if (PC == 0xFFBD && IsKernal(PC)) // SETNAM
            {
                // set to name BANK (reference $FF68 JSETBNK)
                var save_mcr = memory[0xFF00];
                memory[0xFF00] = 0; // switch in KERNAL where mcr table is located
                memory[0xFF00]= memory[(ushort)(0xF7F0 + memory[0xC7])]; // switch to name bank
                
                var result = base.ExecutePatch(); // DELEGATE TO emucbm

                memory[0xFF00] = save_mcr; // restore MCR
                return result;
            }
            else if ((PC == 0xFFD5 || PC == 0xFFD8) && IsKernal(PC)) // LOAD OR SAVE
            {
                // set to data i/o BANK
                var save_mcr = memory[0xFF00];
                memory[0xFF00] = 0; // switch in KERNAL where mcr table is located
                memory[0xFF00] = memory[(ushort)(0xF7F0 + memory[0xC6])]; // switch to i/o bank

                var result = base.ExecutePatch(); // DELEGATE TO emucbm

                memory[0xFF00] = save_mcr; // restore MCR
                return result;
            }
            else if ((PC == 0x4D37 || PC == LOAD_TRAP) && (IsBasicLow(PC) || IsBasicHigh(PC))) // READY
            {
                //go_state = 0;

                if (startup_state == 0 && (StartupPRG != null || PC == LOAD_TRAP))
                {
                    bool is_basic;
                    if (PC == LOAD_TRAP)
                    {
                        is_basic = (
                            FileVerify == false
                            && FileSec == 0 // relative load, not absolute
                            && LO(FileAddr) == memory[45] // requested load address matches BASIC start
                            && HI(FileAddr) == memory[46]);
                        if (FileLoad(out byte err))
                        {
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
                        FileAddr = (ushort)(memory[45] | (memory[46] << 8));
                        is_basic = LoadStartupPrg();
                        //memory[0xAE] = (byte)FileAddr;
                        //memory[0xAF] = (byte)(FileAddr >> 8);
                    }

                    StartupPRG = null;

                    if (is_basic)
                    {
                        // initialize first couple bytes (may only be necessary for UNNEW?)
                        ushort addr = (ushort)(memory[45] | (memory[46] << 8));
                        memory[addr] = 1;
                        memory[(ushort)(addr + 1)] = 1;

                        startup_state = 1; // should be able to regain control when returns...

                        return ExecuteJSR(0xAF87); // LINKPRG
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
                    ushort addr = (ushort)(memory[0x24] | (memory[0x25] << 8) + 2);
                    memory[47] = (byte)addr;
                    memory[48] = (byte)(addr >> 8);

                    SetA(0);

                    startup_state = 2; // should be able to regain control when returns...

                    return ExecuteJSR(0x51F8); // CLEAR/CLR
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
            else if (PC == 0x05A4A) // GO next token is not TO, used to catch 2001 as ASCII
            {
                ushort addr = (ushort)(memory[0x3D] | (memory[0x3E] << 8)); // pointer to current token in buffer
                var s = new StringBuilder();
                while (s.Length < 80) // some limit
                {
                    char c = (char)memory[addr++];
                    if (c >= '0' && c <= '9')
                        s.Append(c);
                    else if (c == 0 || s.Length > 0)
                        break;
                }
                ushort go_num;
                if (ushort.TryParse(s.ToString(), out go_num) && go_num == 2001)
                {
                    Program.go_num = go_num;
                    exit = true;
                    return true;
                }
            }
            else if (PC == 0x05A4D) // GO value expression evaluated to byte stored in .X, catch other byte values we want that are not 64
            {
                if (X == 20 || X == 4 || X == 16 || X == 128)
                {
                    Program.go_num = X;
                    exit = true;
                    return true;
                }
            }
            //else if (PC == 0xC815) // Execute after GO
            //{
            //    if (go_state == 0 && A >= (byte)'0' && A <= (byte)'9')
            //    {
            //        go_state = 1;
            //        return ExecuteJSR(0xCD8A); // Evaluate expression, check data type
            //    }
            //    else if (go_state == 1)
            //    {
            //        go_state = 2;
            //        return ExecuteJSR(0xD7F7); // Convert fp to 2 byte integer
            //    }
            //    else if (go_state == 2)
            //    {
            //        Program.go_num = (ushort)(Y + (A << 8));
            //        exit = true;
            //        return true;
            //    }
            //}

            if (Program.go_num == 64)
            {
                exit = true;
                return true;
            }

            return base.ExecutePatch();
        }

        public bool IsBasicLow(ushort addr)
            => ((C128Memory)memory).IsBasicLow(addr);

        public bool IsBasicHigh(ushort addr)
            => ((C128Memory)memory).IsBasicHigh(addr);

        public bool IsKernal(ushort addr) 
            => ((C128Memory)memory).IsKernal(addr);

        public bool IsRam(ref int addr)
            => ((C128Memory)memory).IsRam(ref addr);

        private void CheckBypassSETNAM()
        {
            var save_mcr = memory[0xFF00];
            memory[0xFF00] = 0; // switch in KERNAL where mcr table is located

            // In case caller bypassed calling SETNAM, get from lower memory
            byte name_len = memory[0xB7];
            ushort name_addr = (ushort)(memory[0xBB] | (memory[0xBC] << 8));
            StringBuilder name = new StringBuilder();

            memory[0xFF00] = memory[(ushort)(0xF7F0 + memory[0xC7])]; // switch to name bank

            for (byte i = 0; i < name_len; ++i)
                name.Append((char)memory[(ushort)(name_addr + i)]);

            memory[0xFF00] = save_mcr; // restore MCR

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

        ///////////////////////////////////////////////////////////////////////

        class C128Memory : Emu6502.Memory
        {
            byte[] ram; // 128K, banked
            byte[] basic_rom_lo;
            byte[] basic_rom_hi;
            byte[] chargen_rom;
            byte[] kernal_rom;
            byte[] io;
            VDC8563 vdc = new VDC8563();

            // note ram starts at 0x0000
            const int basic_lo_addr = 0x4000;
            const int basic_lo_size = 0x4000;
            const int basic_hi_addr = 0x8000;
            const int basic_hi_size = 0x4000;
            const int kernal_addr = 0xC000;
            const int io_addr = 0xD000;
            const int io_size = 0x1000;
            const int color_addr = 0xD800;
            const int color_size = 0x0400;
            const int mmu_addr = 0xD500;
            const int mmu_size = 0xC;
            const int chargen_addr = io_addr;
            const int chargen_size = io_size;

            public void ApplyColor()
            {
                bool reverse = (this[243] != 0);
                
#if CBM_COLOR
                if (reverse)
                {
                    Console.BackgroundColor = ToConsoleColor(this[241]);
                    Console.ForegroundColor = ToConsoleColor(this[0xD021]);
                }
                else
                {
                    Console.ForegroundColor = ToConsoleColor(this[241]);
                    Console.BackgroundColor = ToConsoleColor(this[0xD021]);
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

            private ConsoleColor ToConsoleColor(byte CommodoreColor)
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

            public C128Memory(string basic_lo_file, string basic_hi_file, string chargen_file, string kernal_file)
            {
                ram = new byte[128 * 1024];
                basic_rom_lo = File.ReadAllBytes(basic_lo_file);
                basic_rom_hi = File.ReadAllBytes(basic_hi_file);
                kernal_rom = File.ReadAllBytes(kernal_file);
                chargen_rom = File.ReadAllBytes(chargen_file);

                for (int i = 0; i < ram.Length; ++i)
                    ram[i] = 0;

                io = new byte[io_size];
                for (int i = 0; i < io.Length; ++i)
                    io[i] = 0x0;

                io[mmu_addr - io_addr] = 0; // default MMU CR
                io[0xD505 - io_addr] = 0xB9; // 40/80 up, no /GAME, no /EXROM, C128 mode, Fast serial out, 8502 select
                io[0xD506 - io_addr] = 0; // no common RAM at startup
                io[0xD507 - io_addr] = 0; // zero page default
                io[0xD508 - io_addr] = 0; // zero page bank
                io[0xD509 - io_addr] = 1; // stack page default
                io[0xD50A - io_addr] = 0; // stack page bank
                io[0xD50B - io_addr] = 0x20; // MMU verison register value 128K, verison 0

                io[0xDC00 - io_addr] = 0xFF; // CIA #1 PORT A
                io[0xDC01 - io_addr] = 0xFF; // CIA #1 PORT B
                io[0xDD00 - io_addr] = 0xFF; // CIA #2 PORT A including SERIAL CLK/DATA INPUT pulled HIGH, no devices present
                io[0xDD01 - io_addr] = 0xFF; // CIA #2 PORT B
            }

            public byte this[ushort addr]
            {
                get
                {
                    int addr128k = addr;
                    if (addr >= 0xFF00 && addr <= 0xFF04)
                        return io[mmu_addr + (addr & 0xF) - io_addr];
                    else if (IsRam(ref addr128k))
                        return ram[addr128k];
                    else if (IsIO(addr))
                    {
                        if (IsColor(addr))
                            return (byte)((io[addr - io_addr] & 0xF) | 0xF0);
                        else if (addr == 0xD011)
                            io[addr - io_addr] ^= 0x80; // toggle 9th raster line bit, so seems like raster is moving
                        else if (addr == 0xD600)
                            return vdc.AddressRegister;
                        else if (addr == 0xD601)
                            return vdc.DataRegister;
                        
                        return io[addr - io_addr];
                    }
                    else if (IsBasicLow(addr))
                        return basic_rom_lo[addr - basic_lo_addr];
                    else if (IsBasicHigh(addr))
                        return basic_rom_hi[addr - basic_hi_addr];
                    else if (IsChargen(addr))
                        return chargen_rom[addr - chargen_addr];
                    else if (IsKernal(addr))
                        return kernal_rom[addr - kernal_addr];
                    else
                        return 0xFF;
                }

                set
                {
                    if (addr == 0xFF00) // CR mirror
                        io[mmu_addr - io_addr] = value; // CR
                    else if (addr == 0xFF01) // LCRA
                        io[mmu_addr - io_addr] = io[mmu_addr - io_addr + 1];
                    else if (addr == 0xFF02) // LCRA
                        io[mmu_addr - io_addr] = io[mmu_addr - io_addr + 2];
                    else if (addr == 0xFF03) // LCRA
                        io[mmu_addr - io_addr] = io[mmu_addr - io_addr + 3];
                    else if (addr == 0xFF04) // LCRA
                        io[mmu_addr - io_addr] = io[mmu_addr - io_addr + 4];
                    else if (IsIO(addr))
                    {
                        if (addr == 0xD021) // background
                        {
                            io[addr - io_addr] = (byte)((value & 0xF) | 0xF0); // store value so can be retrieved
                            ApplyColor();
                        }
                        else if (addr == 0xD505)
                        {
                            System.Diagnostics.Debug.WriteLine($"Mode Configuration Register set 0x{value:X02}");
                            if ((value & 0x40) != 0)
                                Program.go_num = 64;
                        }
                        else if (addr >= mmu_addr && addr < mmu_addr + mmu_size - 1) // MMU up to but not including version register
                            io[addr - io_addr] = value;
                        else if (addr == 0xD600)
                            vdc.AddressRegister = value;
                        else if (addr == 0xD601)
                            vdc.DataRegister = value;
                        // but do not set other I/O values
                    }
                    else
                    {
                        int addr128k = addr;
                        if (IsRam(ref addr128k, isWrite: true))
                        {
                            ram[addr128k] = value;
                            if (addr128k == 241 || addr128k == 243)
                                ApplyColor();
                        }
                    }
                }
            }

            private bool IsChargen(ushort addr)
            {
                byte mmu_cr = io[mmu_addr - io_addr];
                return (addr >= chargen_addr && addr < chargen_addr + chargen_size && (mmu_cr & 0x30) == 0);
            }

            public bool IsKernal(ushort addr)
            {
                byte mmu_cr = io[mmu_addr - io_addr];
                return (addr >= kernal_addr && !(addr >= chargen_addr && addr < chargen_addr + chargen_size) && (mmu_cr & 0x30) == 0);
            }

            public bool IsBasicHigh(ushort addr)
            {
                byte mmu_cr = io[mmu_addr - io_addr];
                return (addr >= basic_hi_addr && addr < basic_hi_addr + basic_hi_size && (mmu_cr & 0x0C) == 0);
            }

            public bool IsBasicLow(ushort addr)
            {
                byte mmu_cr = io[mmu_addr - io_addr];
                return (addr >= basic_lo_addr && addr < basic_lo_addr + basic_lo_size && (mmu_cr & 0x02) == 0);
            }

            public bool IsColor(ushort addr)
            {
                return IsIO(addr) && addr >= color_addr && addr < color_addr + color_size;
            }

            public bool IsIO(ushort addr)
            {
                byte mmu_cr = io[mmu_addr - io_addr];
                return (addr >= io_addr && addr < io_addr + io_size && (mmu_cr & 0x01) == 0);
            }

            public bool IsRam(ref int addr, bool isWrite = false)
            {
                byte mmu_cr = io[mmu_addr - io_addr]; // MMU configuration register
                byte ram_cr = io[0xD506 - io_addr]; // RAM configuration register
                int page0_addr = (io[0xD507 - io_addr] | (io[0xD508 - io_addr] << 8)) << 8;
                int page1_addr = (io[0xD509 - io_addr] | (io[0xD50A - io_addr] << 8)) << 8;
                // note: ignore (mmu_cr & 0x80) != 0 because expansion RAM not implemented in hardware
                if (addr >= kernal_addr && (mmu_cr & 0x30) != 0x30 && !isWrite)
                    return false;
                if (addr >= basic_hi_addr && addr < basic_hi_addr + basic_hi_size && (mmu_cr & 0x0C) != 0x0C && !isWrite)
                    return false;
                if (addr >= basic_lo_addr && addr < basic_lo_addr + basic_lo_size && (mmu_cr & 0x02) != 0x02 && !isWrite)
                    return false;
                if (addr >= io_addr && addr < io_addr + io_size && (mmu_cr & 0x01) != 0x01)
                    return false;

                // bank 1
                if ((mmu_cr & 0x40) != 0)
                    addr |= 0x10000;

                // remap/swap zero page and stack
                if (addr >= page0_addr && addr < page0_addr + 0x100)
                {
                    addr = (addr & 0xFF) | (page0_addr & 0x10000);
                }
                else if (addr >= page1_addr && addr < page1_addr + 0x100)
                {
                    addr = (addr & 0xFF) | 0x100 | (page1_addr & 0x10000);
                }
                else if ((ushort)addr < 0x100)
                {
                    addr |= page0_addr;
                }
                else if ((ushort)addr >= 0x100 && (ushort)addr < 0x200)
                {
                    addr = (addr & 0xFF) | page1_addr;
                }

                var hasCommonRam = ((ram_cr & 0x0C) != 0);
                if (hasCommonRam && addr >= 0x10000)
                {
                    int size;
                    switch (ram_cr & 3)
                    {
                        case 0: size = 1024; break;
                        case 1: size = 4096; break;
                        case 2: size = 8192; break;
                        case 3: size = 16384; break;
                        default: throw new Exception("shouldn't happen");
                    }

                    var isBottomShared = ((ram_cr & 4) != 0);
                    var isTopShared = ((ram_cr & 8) != 0);

                    if (isBottomShared && (ushort)addr < size)
                        addr = (ushort)addr; // common RAM is in BANK 0
                    else if (isTopShared && (ushort)addr + size >= 0x10000)
                        addr = (ushort)addr; // common RAM is in BANK 0
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        class VDC8563
        {
            byte[] registers;
            byte[] ram;
            int register;
            byte data;
            bool ready = false;

            public VDC8563()
            {
                registers = new byte[]
                { 
                    126, 80, 102, 73, 32, 224, 25, 29, 
                    252, 231, 160, 231, 0, 0, 0, 0, 
                    0, 0, 15, 228, 8, 0, 120, 232,
                    32, 71, 240, 0, 63, 21, 79, 0,
                    0, 0, 125, 100, 245, 63  
                };
                
                ram = new byte[64 * 1024];
                for (int i = 0; i < ram.Length; ++i)
                    ram[i] = 0;
            }

            public byte AddressRegister
            {
                get
                {
                    if (ready)
                    {
                        return 128;
                    }
                    else
                    {
                        ready = true; // simulate delay in processing
                        return 0;
                    }
                }

                set
                {
                    register = value & 0x3F;
                    if (register < registers.Length)
                        data = registers[register];
                    else
                        data = 0xFF;
                    ready = false; // simulate delay in processing
                }
            }

            public byte DataRegister
            {
                get
                {
                    if (ready)
                        return data;
                    else
                    {
                        ready = true;
                        return 0xFF;
                    }
                }
                set
                {
                    ready = false; // simulate delay in processing

                    if (register == 5 || register == 9 || register == 11 || register == 23 || register == 29)
                        data &= 0x1F; // only 5 bits
                    else if (register == 8)
                        data &= 3; // only 2 bits
                    else if (register == 10)
                        register &= 0x7F; // only 7 bits
                    else if (register == 28)
                        register &= 0xF0; // only upper 4 bits
                    else if (register == 36)
                        register &= 0x0F; // only 4 bits
                    else if (register == 37)
                        register &= 0x3F; // only 6 bits

                    if (register < registers.Length)
                    {
                        registers[register] = data;

                        if (register == 31)
                        {
                            ushort dest = (ushort)(registers[18] + (registers[19] << 8));
                            if ((registers[24] & 0x80) == 0)
                                ram[dest++] = data;
                            else
                            {
                                ushort src = (ushort)(registers[32] + (registers[33] << 8));
                                ram[dest++] = ram[src++];
                                registers[32] = (byte)src;
                                registers[33] = (byte)(src >> 8);
                            }
                            registers[18] = (byte)dest;
                            registers[19] = (byte)(dest >> 8);
                        }
                        else if (register == 30)
                        {
                            ushort dest = (ushort)(registers[18] + (registers[19] << 8));
                            if ((registers[24] & 0x80) == 0)
                            {
                                for (int i = 0; i < value; ++i)
                                    ram[dest++] = registers[31];
                            }
                            else
                            {
                                ushort src = (ushort)(registers[32] + (registers[33] << 8));
                                for (int i = 0; i < value; ++i)
                                    ram[dest++] = ram[src++];
                                registers[32] = (byte)src;
                                registers[33] = (byte)(src >> 8);
                            }
                            registers[18] = (byte)dest;
                            registers[19] = (byte)(dest >> 8);
                        }
                    }
                }
            }
        }
    }
}
