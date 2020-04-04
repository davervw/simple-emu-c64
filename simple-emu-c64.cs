// simple-emu-c64.cs - Program.Main()
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright(c) 2020 by David R.Van Wagner ALL RIGHTS RESERVED
// davevw.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace simple_emu_c64
{
    class Program
    {
        static void Main(string[] args)
        {
            // recommend get basic, kernal, etc. ROM files from a emulator such as https://vice-emu.sourceforge.io/index.html#download
            Emu6502 cbm = null;
            bool error = false;
            int ram_size = 0;

            Console.Error.WriteLine("6502 Emulator for Microsoft Windows Console: C64, VIC-20, TED, ...");
            Console.Error.WriteLine("simple-emu-c64 version 1.3");
            Console.Error.WriteLine("Copyright (c) 2020 by David R. Van Wagner ALL RIGHTS RESERVED");
            Console.Error.WriteLine("davevw.com");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("Open Source, MIT License: github.com/davervw/simple-emu-c64");
            Console.Error.WriteLine("");
            System.Threading.Thread.Sleep(2000);

            try
            {
                if (args.Length > 2 && args[1].ToLower() == "ram")
                    ram_size = int.Parse(args[2]) * 1024;

                if (args.Length == 0 || args[0].ToLower() == "c64")
                {
                    if (ram_size == 0)
                        ram_size = 64 * 1024;
                    if (File.Exists("basic") && File.Exists("kernal") && (!File.Exists("c64\\basic") || !File.Exists("c64\\kernal")))
                        cbm = new EmuC64(ram_size: ram_size, basic_file: "basic", kernal_file: "kernal");
                    else
                        cbm = new EmuC64(ram_size: ram_size, basic_file: "c64\\basic", kernal_file: "c64\\kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "vic20")
                {
                    cbm = new EmuVIC20(ram_size: ram_size, char_file: "vic20\\chargen", basic_file: "vic20\\basic", kernal_file: "vic20\\kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "c16")
                {
                    if (ram_size == 0)
                        ram_size = 16 * 1024;
                    cbm = new EmuTED(ram_size: ram_size, basic_file: "ted\\basic", kernal_file: "ted\\kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "plus4" || args[0].ToLower() == "ted")
                {
                    if (ram_size == 0)
                        ram_size = 64 * 1024;
                    cbm = new EmuTED(ram_size: ram_size, basic_file: "ted\\basic", kernal_file: "ted\\kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "pet")
                {
                    cbm = new EmuPET(basic_file: "pet\\basic2", edit_file: "pet\\edit2b", kernal_file: "pet\\kernal2");
                }
                else
                {
                    error = true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                error = true;
            }

            if (error)
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  simple-emu-c64     (with no arguments defaults to C64)");
                Console.Error.WriteLine("  simple-emu-c64 c64 [ram #]|[walk [addr1 ...]]");
                Console.Error.WriteLine("  simple-emu-c64 vic20 [ram 4[-39]]|[walk [addr1 ...]]");
                Console.Error.WriteLine("  simple-emu-c16 c16 [ram 16[-64]]|[walk [addr1 ...]]");
                Console.Error.WriteLine("  simple-emu-c16 plus4 [ram 16[-64]]|[walk [addr1 ...]]");
                Console.Error.WriteLine("  simple-emu-c16 ted [ram 16[-64]]|[walk [addr1 ...]]");
                Console.Error.WriteLine("  (with appropriate roms in c64, vic20, or ted folder)");
                Console.Error.WriteLine("  (note: only certain ram sizes are acceptable)");
                return;
            }

            if (args.Length >= 2 && args[1].ToLower() == "walk")
            {
                if (args.Length == 2)
                    cbm.Walk();
                else
                {
                    Walk6502.Reset();
                    for (int i = 2; i < args.Length; ++i)
                        Walk6502.Walk(cbm, ParseAddr(args[i]));
                }
            }
            else
            {
                cbm.ResetRun();
            }
        }

        static ushort ParseAddr(string str_addr)
        {
            try
            {
                if (str_addr.ToLower().StartsWith("0x"))
                    str_addr = str_addr.Substring(2);
                else if (str_addr.StartsWith("$") || str_addr.StartsWith("x"))
                    str_addr = str_addr.Substring(1);

                return ushort.Parse(str_addr, System.Globalization.NumberStyles.HexNumber);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                throw new InvalidOperationException("Hex address expected, optionally preceeded by $ or 0x or x");
            }
        }
    }
}
