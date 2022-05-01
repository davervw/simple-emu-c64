﻿// simple-emu-c64.cs - Program.Main()
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright(c) 2020 by David R.Van Wagner
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
        static public ushort go_num = 0;

        static void Main(string[] args)
        {
            // recommend get basic, kernal, etc. ROM files from a emulator such as https://vice-emu.sourceforge.io/index.html#download
            Emu6502 cbm = null;
            bool error = false;
            int ram_size = 0;

            Console.Error.WriteLine("6502 Emulator for Windows Console");
            Console.Error.WriteLine("C64, VIC-20, PET, TED, C128, ...");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("simple-emu-c64 version 1.8");
            Console.Error.WriteLine("Copyright (c) 2020-2022 David R. Van Wagner");
            Console.Error.WriteLine("davevw.com");
            Console.Error.WriteLine("Open Source, MIT License");
            Console.Error.WriteLine("github.com/davervw/simple-emu-c64");
            Console.Error.WriteLine("");

            try
            {
                if (args.Length > 2 && args[1].ToLower() == "ram")
                    ram_size = int.Parse(args[2]) * 1024;

                if (args.Length == 0 || args[0].ToLower() == "c64")
                {
                    if (ram_size == 0)
                        ram_size = 64 * 1024;
                    if (File.Exists("basic") && File.Exists("kernal") && (!File.Exists($"c64{Path.DirectorySeparatorChar}basic") || !File.Exists($"c64{Path.DirectorySeparatorChar}kernal")))
                        cbm = new EmuC64(ram_size: ram_size, basic_file: "basic", chargen_file: $"c64{Path.DirectorySeparatorChar}chargen", kernal_file: "kernal");
                    else
                        cbm = new EmuC64(ram_size: ram_size, basic_file: $"c64{Path.DirectorySeparatorChar}basic", chargen_file: $"c64{Path.DirectorySeparatorChar}chargen", kernal_file: $"c64{Path.DirectorySeparatorChar}kernal");

                    if ((args.Length == 2 || args.Length == 4) && (File.Exists(args[args.Length-1]) || File.Exists(args[args.Length - 1]+".prg")))
                        ((EmuC64)cbm).StartupPRG = args[args.Length - 1];
                }
                else if (args.Length > 0 && args[0].ToLower() == $"vic20")
                {
                    cbm = new EmuVIC20(ram_size: ram_size, char_file: $"vic20{Path.DirectorySeparatorChar}chargen", basic_file: $"vic20{Path.DirectorySeparatorChar}basic", kernal_file: $"vic20{Path.DirectorySeparatorChar}kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "c16")
                {
                    if (ram_size == 0)
                        ram_size = 16 * 1024;
                    cbm = new EmuTED(ram_size: ram_size, basic_file: $"ted{Path.DirectorySeparatorChar}basic", kernal_file: $"ted{Path.DirectorySeparatorChar}kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "plus4" || args[0].ToLower() == "ted")
                {
                    if (ram_size == 0)
                        ram_size = 64 * 1024;
                    cbm = new EmuTED(ram_size: ram_size, basic_file: $"ted{Path.DirectorySeparatorChar}basic", kernal_file: $"ted{Path.DirectorySeparatorChar}kernal");
                }
                else if (args.Length > 0 && args[0].ToLower() == "pet")
                {
                    if (ram_size == 0)
                        ram_size = 8 * 1024;
                    cbm = new EmuPET(ram_size: ram_size, basic_file: $"pet{Path.DirectorySeparatorChar}basic1", edit_file: $"pet{Path.DirectorySeparatorChar}edit1g", kernal_file: $"pet{Path.DirectorySeparatorChar}kernal1");
                }
                else if (args.Length > 0 && args[0].ToLower() == "c128")
                {
                    cbm = new EmuC128(basic_lo_file: $"c128{Path.DirectorySeparatorChar}basiclo", basic_hi_file: $"c128{Path.DirectorySeparatorChar}basichi", chargen_file: $"c128{Path.DirectorySeparatorChar}chargen", kernal_file: $"c128{Path.DirectorySeparatorChar}kernal");
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
                return;
            else if (args.Length == 0) // if no arguments present, then show usage as well
            {
                Console.Error.WriteLine("");
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  simple-emu-c64                     (no arguments pauses, then starts c64)");
                Console.Error.WriteLine("  simple-emu-c64 help                (shows usage)");
                Console.Error.WriteLine("  simple-emu-c64 [system] {ram #}    (system=[c64|vic20|pet|c16|plus4|ted|c128])");
                Console.Error.WriteLine("  simple-emu-c64 [system] walk [addr 1 ...]");
                Console.Error.WriteLine("");
                Console.WriteLine();
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
                while (true)
                {
                    cbm.ResetRun();
                    if (go_num == 0)
                    {
                        Console.WriteLine("BYE.");
                        break;
                    }
                    if (go_num != 2001 && go_num != 20 && go_num != 64 && go_num != 16 && go_num != 4)
                    {
                        Console.WriteLine("INVALID QUANTITY  ERROR");
                        break;
                    }
                    else
                    {
                        Console.Write("RAM (in kilobytes)? ");
                        string line = Console.ReadLine();
                        float ram_kilobytes;
                        if (!float.TryParse(line, out ram_kilobytes))
                        {
                            Console.WriteLine("TYPE MISMATCH  ERROR");
                            break;
                        }
                        else
                        {
                            ram_size = (int)(ram_kilobytes * 1024);
                            try
                            {
                                if (go_num == 2001)
                                    cbm = new EmuPET(ram_size: ram_size, basic_file: $"pet{Path.DirectorySeparatorChar}basic1", edit_file: $"pet{Path.DirectorySeparatorChar}edit1g", kernal_file: $"pet{Path.DirectorySeparatorChar}kernal1");
                                else if (go_num == 20)
                                    cbm = new EmuVIC20(ram_size: ram_size, char_file: $"vic20{Path.DirectorySeparatorChar}chargen", basic_file: $"vic20{Path.DirectorySeparatorChar}basic", kernal_file: $"vic20{Path.DirectorySeparatorChar}kernal");
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
