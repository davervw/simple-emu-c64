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
            // recommend get basic, kernal ROM files from a C64 emulator such as https://vice-emu.sourceforge.io/index.html#download
            Emu6502 cbm = null;
            bool error = false;

            try
            {
                if (args.Length == 0 || args[0] == "c64")
                {
                    if (File.Exists("basic") && File.Exists("kernal") && (!File.Exists("c64\\basic") || !File.Exists("c64\\kernal")))
                        cbm = new EmuC64(basic_file: "basic", kernal_file: "kernal");
                    else
                        cbm = new EmuC64(basic_file: "c64\\basic", kernal_file: "c64\\kernal");
                }
                else if (args.Length > 0 && args[0] == "vic20")
                {
                    cbm = new EmuVIC20(char_file: "vic20\\chargen", basic_file: "vic20\\basic", kernal_file: "vic20\\kernal");
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
                Console.Error.WriteLine("  simple-emu-c64 c64 [walk [addr1 ...]]");
                Console.Error.WriteLine("  simple-emu-c64 vic20 [walk [addr1 ...]]");
                Console.Error.WriteLine("  (with appropriate roms in c64 or vic20 folder)");
                return;
            }

            if (args.Length >= 2 && args[1] == "walk")
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
