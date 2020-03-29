// walk6502.cs - Class Walk6502
// disassembly of all reachable executable code including branches, jump, calls
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
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace simple_emu_c64
{
    class Walk6502
    {
        static HashSet<ushort> seen = new HashSet<ushort>();

        public static void Reset()
        {
            seen.Clear();
        }

        public static void Walk(Emu6502 cpu, ushort addr)
        {
            bool conditional;
            byte bytes;
            ushort addr2;
            HashSet<ushort> branches = new HashSet<ushort>();

            while (true)
            {
                if (seen.Contains(addr))
                {
                    while (true)
                    {
                        if (branches.Count == 0)
                            return; // done with walk
                        else
                        {
                            addr = branches.First(); // walk a saved address
                            branches.Remove(addr);
                            if (!seen.Contains(addr))
                                break;
                        }
                    }
                }
                string line;
                string dis = cpu.Disassemble(addr, out conditional, out bytes, out addr2, out line);
                Console.WriteLine(line);
                if (dis != "???")
                    seen.Add(addr);

                switch (dis)
                {
                    case "BRK":
                    case "RTI":
                    case "RTS":
                    case "???":
                        if (branches.Count == 0)
                            return; // done with walk
                        else
                        {
                            addr = branches.First(); // walk a saved address
                            branches.Remove(addr);
                            break;
                        }

                    default:
                        if (!conditional && addr2 != 0)
                        {
                            if (dis.StartsWith("JSR"))
                            {
                                Walk(cpu, addr2); // walk call recursively, then continue next address
                                addr += bytes;
                            }
                            else
                                addr = addr2;
                        }
                        else
                        {
                            addr += bytes;
                            if (conditional && !seen.Contains(addr2) && !branches.Contains(addr2))
                                branches.Add(addr2); // save branch address for later
                        }
                        break;
                }
            }
        }

    }
}
