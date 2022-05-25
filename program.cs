// simple-emu-c64.cs - Program.Main()
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright (c) 2020-2022 by David R.Van Wagner
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
using System.Collections.Generic;
using System.IO;

namespace simple_emu_c64
{
    class Program
    {
        static public ushort go_num = 0;

        private enum CBMmodel
        { 
            invalid = 0,
            c64 = 1, 
            vic20 = 2, 
            pet = 3, 
            ted = 4, 
            c16 = 5, 
            plus4 = 6, 
            c128 = 7,
        };

        private enum Keyword
        {
            unspecified = 0,
            help = 1,
            ram = 2,
            walk = 3,
        }

        static void Main(string[] args)
        {
            // recommend get basic, kernal, etc. ROM files from a emulator such as https://vice-emu.sourceforge.io/index.html#download
            Emu6502 cbm = null;
            bool error = false;
            int ram_size = 0;
            var model = CBMmodel.invalid;
            var keyword = Keyword.unspecified;
            var walkAddrs = new List<ushort>();
            var startupPRG = null as string;
            var encodingSpecified = false;
            CBM_Console.CBMEncoding encoding = CBM_Console.CBMEncoding.ascii;

            Console.Error.WriteLine("6502 Emulator for Windows Console");
            Console.Error.WriteLine("C64, VIC-20, PET, TED, C128, ...");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("simple-emu-c64 version 1.8.6");
            Console.Error.WriteLine("Copyright (c) 2022 David R. Van Wagner");
            Console.Error.WriteLine("davevw.com");
            Console.Error.WriteLine("Open Source, MIT License");
            Console.Error.WriteLine("github.com/davervw/simple-emu-c64");
            Console.Error.WriteLine("");

            try
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    if (model == CBMmodel.invalid && Enum.TryParse(args[i], ignoreCase: true, out model))
                    {
                        if (model == CBMmodel.invalid)
                        {
                            error = true;
                            break;
                        }
                        continue;
                    }
                    if (keyword == Keyword.unspecified && Enum.TryParse(args[i], ignoreCase: true, out keyword))
                    {
                        if (keyword == Keyword.walk)
                        {
                            for (int j = i + 1; j < args.Length; ++j)
                                walkAddrs.Add(ParseAddr(args[j]));
                            break;
                        }
                        if (keyword == Keyword.ram)
                        {
                            ram_size = int.Parse(args[++i]) * 1024;
                            continue;
                        }
                    }
                    if (!encodingSpecified && Enum.TryParse(args[i], ignoreCase: true, out encoding))
                    {
                        encodingSpecified = true;
                        continue;
                    }
                    if (File.Exists(args[i]) || File.Exists(args[i] + ".PRG"))
                    {
                        startupPRG = args[i];
                        continue;
                    }
                    error = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                error = true;
            }

            if (model == CBMmodel.invalid)
                model = CBMmodel.c64;

            if (model == CBMmodel.c64)
            {
                if (ram_size == 0)
                    ram_size = 64 * 1024;
                if (File.Exists("basic") && File.Exists("kernal") && (!File.Exists($"c64{Path.DirectorySeparatorChar}basic") || !File.Exists($"c64{Path.DirectorySeparatorChar}kernal")))
                    cbm = new EmuC64(ram_size: ram_size, basic_file: "basic", chargen_file: $"c64{Path.DirectorySeparatorChar}chargen", kernal_file: "kernal");
                else
                    cbm = new EmuC64(ram_size: ram_size, basic_file: $"c64{Path.DirectorySeparatorChar}basic", chargen_file: $"c64{Path.DirectorySeparatorChar}chargen", kernal_file: $"c64{Path.DirectorySeparatorChar}kernal");

                ((EmuC64)cbm).StartupPRG = startupPRG;
            }
            else if (model == CBMmodel.vic20)
            {
                cbm = new EmuVIC20(ram_size: ram_size, char_file: $"vic20{Path.DirectorySeparatorChar}chargen", basic_file: $"vic20{Path.DirectorySeparatorChar}basic", kernal_file: $"vic20{Path.DirectorySeparatorChar}kernal");
            }
            else if (model == CBMmodel.c16)
            {
                if (ram_size == 0)
                    ram_size = 16 * 1024;
                cbm = new EmuTED(ram_size: ram_size, basic_file: $"ted{Path.DirectorySeparatorChar}basic", kernal_file: $"ted{Path.DirectorySeparatorChar}kernal");

                ((EmuTED)cbm).StartupPRG = startupPRG;
            }
            else if (model == CBMmodel.plus4 || model == CBMmodel.ted)
            {
                if (ram_size == 0)
                    ram_size = 64 * 1024;
                cbm = new EmuTED(ram_size: ram_size, basic_file: $"ted{Path.DirectorySeparatorChar}basic", kernal_file: $"ted{Path.DirectorySeparatorChar}kernal");

                ((EmuTED)cbm).StartupPRG = startupPRG;
            }
            else if (model == CBMmodel.pet)
            {
                if (ram_size == 0)
                    ram_size = 8 * 1024;
                cbm = new EmuPET(ram_size: ram_size, basic_file: $"pet{Path.DirectorySeparatorChar}basic1", edit_file: $"pet{Path.DirectorySeparatorChar}edit1g", kernal_file: $"pet{Path.DirectorySeparatorChar}kernal1");
            }
            else if (model == CBMmodel.c128)
            {
                cbm = new EmuC128(basic_lo_file: $"c128{Path.DirectorySeparatorChar}basiclo", basic_hi_file: $"c128{Path.DirectorySeparatorChar}basichi", chargen_file: $"c128{Path.DirectorySeparatorChar}chargen", kernal_file: $"c128{Path.DirectorySeparatorChar}kernal");
            }

            if (cbm != null && encodingSpecified)
                CBM_Console.Encoding = encoding;

            if (args.Length == 0 || error || keyword == Keyword.help) // if no arguments present, then show usage as well
            {
                Console.Error.WriteLine("");
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  simple-emu-c64                     (no arguments pauses, then starts c64)");
                Console.Error.WriteLine("  simple-emu-c64 help                (shows usage)");
                Console.Error.WriteLine("  simple-emu-c64 [system] {ram #}    (system=[c64|vic20|pet|c16|plus4|ted|c128])");
                Console.Error.WriteLine("  simple-emu-c64 [system] walk [addr1 ...]");
                Console.Error.WriteLine("  simple-emu-c64 [c64|ted] file      (autorun prg)");
                Console.Error.WriteLine("");
                Console.WriteLine();
            }

            if (error)
                return;

            if (keyword == Keyword.help)
                return;

            if (keyword == Keyword.walk)
            {
                if (walkAddrs.Count == 0)
                {
                    cbm.Walk();
                    return;
                }

                Walk6502.Reset();
                foreach (var addr in walkAddrs)
                    Walk6502.Walk(cbm, addr);
                return;
            }

            while (true)
            {
                cbm.ResetRun();
                if (go_num == 0)
                {
                    Console.WriteLine("BYE.");
                    break;
                }
                if (go_num != 2001 && go_num != 20 && go_num != 64 && go_num != 16 && go_num != 4 && go_num != 128)
                {
                    Console.WriteLine("INVALID QUANTITY  ERROR");
                    break;
                }
                float ram_kilobytes = 0;
                if (go_num != 128)
                {
                    Console.Write("RAM (in kilobytes)? ");
                    string line = Console.ReadLine();

                    if (!float.TryParse(line, out ram_kilobytes))
                    {
                        Console.WriteLine("TYPE MISMATCH  ERROR");
                        break;
                    }
                }
                ram_size = (int)(ram_kilobytes * 1024);
                try
                {
                    if (go_num == 2001)
                        cbm = new EmuPET(ram_size: ram_size, basic_file: $"pet{Path.DirectorySeparatorChar}basic1", edit_file: $"pet{Path.DirectorySeparatorChar}edit1g", kernal_file: $"pet{Path.DirectorySeparatorChar}kernal1");
                    else if (go_num == 20)
                        cbm = new EmuVIC20(ram_size: ram_size, char_file: $"vic20{Path.DirectorySeparatorChar}chargen", basic_file: $"vic20{Path.DirectorySeparatorChar}basic", kernal_file: $"vic20{Path.DirectorySeparatorChar}kernal");
                    else if (go_num == 128)
                        cbm = new EmuC128(basic_lo_file: $"c128{Path.DirectorySeparatorChar}basiclo", basic_hi_file: $"c128{Path.DirectorySeparatorChar}basichi", chargen_file: $"c128{Path.DirectorySeparatorChar}chargen", kernal_file: $"c128{Path.DirectorySeparatorChar}kernal");
                    else if (go_num == 64)
                    {
                        if (File.Exists("basic") && File.Exists("kernal") && (!File.Exists($"c64{Path.DirectorySeparatorChar}basic") || !File.Exists($"c64{Path.DirectorySeparatorChar}kernal")))
                            cbm = new EmuC64(ram_size: ram_size, basic_file: "basic", chargen_file: $"c64{Path.DirectorySeparatorChar}chargen", kernal_file: "kernal");
                        else
                            cbm = new EmuC64(ram_size: ram_size, basic_file: $"c64{Path.DirectorySeparatorChar}basic", chargen_file: $"c64{Path.DirectorySeparatorChar}chargen", kernal_file: $"c64{Path.DirectorySeparatorChar}kernal");
                    }
                    else if (go_num == 16 || go_num == 4)
                    {
                        cbm = new EmuTED(ram_size: ram_size, basic_file: $"ted{Path.DirectorySeparatorChar}basic", kernal_file: $"ted{Path.DirectorySeparatorChar}kernal");
                    }
                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message);
                    break;
                }
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
