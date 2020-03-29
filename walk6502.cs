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

        // Commodore 64 - walk Kernal Reset vector, MAIN, CRUNCH, GONE (EXECUTE), Statements, Functions, and Operators BASIC ROM code
        public static void Walk(Emu6502 cpu)
        {
            byte[] memory = cpu.memory;

            // in case cpu has not been reset, manually initialize low memory that will be called by BASIC and KERNAL ROM

            Array.Copy(memory, 0xE3A2, memory, 0x73, 0x18); // CHRGET

            memory[0x300] = LO(0xE38B); // ERROR
            memory[0x301] = HI(0xE38B);

            memory[0x302] = LO(0xA483); // MAIN
            memory[0x303] = HI(0xA483);

            memory[0x304] = LO(0xA57C); // CRUNCH
            memory[0x305] = HI(0xA57C);

            memory[0x306] = LO(0xA71A); // QPLOP
            memory[0x307] = HI(0xA71A);

            memory[0x308] = LO(0xA7E4); // GONE
            memory[0x309] = HI(0xA7E4);

            memory[0x30A] = LO(0xAE86); // EVAL
            memory[0x30B] = HI(0xAE86);

            memory[0x31A] = LO(0xF34A); // OPEN
            memory[0x31B] = HI(0xF34A);

            memory[0x31C] = LO(0xF291); // CLOSE
            memory[0x31D] = HI(0xF291);

            memory[0x31E] = LO(0xF20E); // CHKIN
            memory[0x31F] = HI(0xF20E);

            memory[0x320] = LO(0xF250); // CKOUT
            memory[0x321] = HI(0xF250);

            memory[0x322] = LO(0xF333); // CHECK STOP
            memory[0x323] = HI(0xF333);

            memory[0x324] = LO(0xF157); // CHRIN
            memory[0x325] = HI(0xF157);

            memory[0x326] = LO(0xF1CA); // CHROUT
            memory[0x327] = HI(0xF1CA);

            memory[0x328] = LO(0xF6ED); // STOP
            memory[0x329] = HI(0xF6ED);

            memory[0x32A] = LO(0xF13E); // GETIN
            memory[0x32B] = HI(0xF13E);

            memory[0x32C] = LO(0xF32F); // CLALL
            memory[0x32D] = HI(0xF32F);

            memory[0x330] = LO(0xF4A5); // LOAD
            memory[0x331] = HI(0xF4A5);

            memory[0x332] = LO(0xF5ED); // SAVE
            memory[0x333] = HI(0xF5ED);

            ushort addr = (ushort)((memory[0xFFFC] | (memory[0xFFFD] << 8))); // RESET vector
            Walk(cpu, addr);

            // Portion of MAIN, CRUNCH and GONE(Execute) or MAIN1(Store line)
            Walk(cpu, 0xA494);

            for (ushort table = 0xA00C; table < 0xA051; table += 2) // BASIC Statements
            {
                addr = (ushort)((memory[table] | (memory[table + 1] << 8)) + 1); // put on stack for RTS, so must add one
                Walk(cpu, addr);
            }

            for (ushort table = 0xA052; table < 0xA07F; table += 2) // Function Dispatch
            {
                addr = (ushort)((memory[table] | (memory[table + 1] << 8)));
                Walk(cpu, addr);
            }

            for (ushort table = 0xA080; table < 0xA09D; table += 3) // Operator Dispatch
            {
                addr = (ushort)((memory[table + 1] | (memory[table + 2] << 8)) + 1); // put on stack for RTS, so must add one
                Walk(cpu, addr);
            }
        }

        private static void Walk(Emu6502 cpu, ushort addr)
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

        static byte LO(ushort value)
        {
            return (byte)value;
        }

        static byte HI(ushort value)
        {
            return (byte)(value >> 8);
        }

    }
}
