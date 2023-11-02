using System;
using System.IO;

namespace simple_emu_c64
{
    public class EmuTest : Emu6502
    {
        // plain 6502, no I/O, just memory to run test code

        bool start;

        // expecting input testfile similar to 6502_functional_test.bin from https://github.com/Klaus2m5/6502_65C02_functional_tests
        // expected behavior is
        // 0) loads all memory starting at address 0x0000 through address 0xFFFF including NMI, RESET, IRQ vectors at end of memory
        // 1) start address of tests is at 0x400 manually patched by ExecutePatch(), not RESET vector
        // 2) active test number stored at 0x200
        // 3) failed test branches with BNE to same instruction to indicate cannot continue
        // 4) IRQs must not be active or IRQ vector catch will fail tests
        // 5) successful completion jumps to same instruction to indicate completion
        public EmuTest(string testfile) : base(new EmuTestMemory(testfile))
        {
            //base.trace = true; // turn on tracing so something to look at, review test listing to see if trapped or successful
            start = true;
        }

        int last_test = -1;

        protected override bool ExecutePatch()
        {
            if (start)
            {
                PC = 0x0400; // start address of tests are 0400
                Console.WriteLine("Start");
                start = false;
                return true;
            }
            if (memory[PC] == 0xD0 && !Z && memory[(ushort)(PC + 1)] == 0xFE)
            {
                Console.WriteLine($"{PC:X4} Test FAIL");
                exit = true;
                return false;
            }
            if (memory[PC] == 0x4C
                && (memory[(ushort)(PC + 1)] == (PC & 0xFF) && memory[(ushort)(PC + 2)] == (PC >> 8)
                || memory[(ushort)(PC + 1)] == 0x00 && memory[(ushort)(PC + 2)] == 0x04)
                )
            {
                Console.WriteLine($"{PC:X4} COMPLETED SUCCESS");
                exit = true;
                return false;
            }
            if (memory[0x200] != last_test)
            {
                last_test = memory[0x200];
                Console.WriteLine($"{PC:X4} Starting test {last_test:X2}");
            }  
            return base.ExecutePatch();
        }

        public class EmuTestMemory : Emu6502.Memory
        {
            const int ram_size = 65536;
            readonly byte[] ram = new byte[ram_size];

            public EmuTestMemory(string testfile)
            {
                for (int i = 0; i < ram_size; ++i)
                    ram[i] = 0;

                var bin = File.ReadAllBytes(testfile);
                Array.Copy(bin, ram, bin.Length);
            }

            public byte this[ushort addr]
            {
                // 0000-7FFF RAM
                // 8000-FFFF ROM
                get { return ram[addr]; }
                set { if (addr < 0x8000) ram[addr] = value; }
            }
        }
    }

}
