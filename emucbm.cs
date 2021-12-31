using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace simple_emu_c64
{
    public class EmuCBM : Emu6502
    {
        protected static readonly ConsoleColor startup_fg = Console.ForegroundColor;
        protected static readonly ConsoleColor startup_bg = Console.BackgroundColor;

        protected string FileName = null;
        protected byte FileNum = 0;
        protected byte FileDev = 0;
        protected byte FileSec = 0;
        protected bool FileVerify = false;
        protected ushort FileAddr = 0;

        protected int LOAD_TRAP = -1;

        public EmuCBM(Emu6502.Memory memory):base(memory)
        {
        }

        public string StartupPRG
        {
            get;
            set;
        }

        protected override bool ExecutePatch()
        {
            if (PC == 0xFFD2) // CHROUT
            {
                CBM_Console.WriteChar((char)A);
                // fall through to draw character in screen memory too
            }
            else if (PC == 0xFFCF) // CHRIN
            {
                SetA(CBM_Console.ReadChar());
                C = false;

                return ExecuteRTS();
            }
            else if (PC == 0xFFE4) // GETIN
            {
                //BASIC TEST:
                //10 GET K$ : REM GETIN
                //20 IF K$<> "" THEN PRINT ASC(K$)
                //25 IF K$= "Q" THEN END
                //30 GOTO 10

                C = false;
                SetA(CBM_Console.GetIn());
                if (A != 0)
                    X = A; // observed this side effect from tracing code, so replicating

                return ExecuteRTS();
            }
            else if (PC == 0xFFE1) // STOP
            {
                Z = CBM_Console.CheckStop();

                return ExecuteRTS();
            }
            else if (PC == 0xFFBA) // SETLFS
            {
                FileNum = A;
                FileDev = X;
                FileSec = Y;
                System.Diagnostics.Debug.WriteLine(string.Format("SETLFS {0},{1},{2}", FileNum, FileDev, FileSec));
            }
            else if (PC == 0xFFBD) // SETNAM
            {
                StringBuilder name = new StringBuilder();
                ushort addr = (ushort)(X + (Y << 8));
                for (int i = 0; i < A; ++i)
                    name.Append((char)memory[(ushort)(addr + i)]);
                System.Diagnostics.Debug.WriteLine(string.Format("SETNAM {0}", name.ToString()));
                FileName = name.ToString();
            }
            else if (PC == 0xFFD5) // LOAD
            {
                FileAddr = (ushort)(X + (Y << 8));
                string op;
                if (A == 0)
                    op = "LOAD";
                else if (A == 1)
                    op = "VERIFY";
                else
                    op = string.Format("LOAD (A={0}) ???", A);
                FileVerify = (A == 1);

                ExecuteRTS();

                if (A == 0 || A == 1)
                {
                    LOAD_TRAP = PC;

                    // Set success
                    C = false;
                }
                else
                {
                    SetA(14); // ILLEGAL QUANTITY message
                    C = true; // failure
                }

                return true; // overriden, and PC changed, so caller should reloop before execution to allow breakpoint/trace/ExecutePatch/etc.
            }
            else if (PC == 0xFFD8) // SAVE
            {
                ushort addr1 = (ushort)(memory[A] + (memory[(ushort)(A + 1)] << 8));
                ushort addr2 = (ushort)(X + (Y << 8));
                System.Diagnostics.Debug.WriteLine(string.Format("SAVE {0:X4}-{1:X4}", addr1, addr2));

                // Set success
                C = !FileSave(FileName, addr1, addr2);

                return ExecuteRTS();
            }
            return false;
        }

        protected bool ExecuteRTS()
        {
            byte unused_bytes;
            RTS(ref PC, out unused_bytes);
            return true; // return value for ExecutePatch so will reloop execution to allow berakpoint/trace/ExecutePatch/etc.
        }

        protected bool ExecuteJSR(ushort addr)
        {
            ushort retaddr = (ushort)(PC - 1);
            Push(HI(retaddr));
            Push(LO(retaddr));
            PC = addr;
            return true; // return value for ExecutePatch so will reloop execution to allow berakpoint/trace/ExecutePatch/etc.
        }

        // returns true if BASIC
        protected bool LoadStartupPrg()
        {
            bool result = FileLoad(out byte unused_err);
            return FileSec == 0 ? true : false; // relative is BASIC, absolute is ML
        }

        // returns success
        protected bool FileLoad(out byte err)
        {
            bool startup = (StartupPRG != null);
            err = 0;
            ushort addr = FileAddr;
            bool success = true;
            try
            {
                string filename = startup ? StartupPRG : FileName;
                if (!File.Exists(filename) && !filename.ToLower().EndsWith(".prg"))
                    filename += ".prg";
                using (FileStream stream = File.OpenRead(filename))
                {
                    byte lo = (byte)stream.ReadByte();
                    byte hi = (byte)stream.ReadByte();
                    if (startup)
                    {
                        if (lo == 1)
                            FileSec = 0;
                        else
                            FileSec = 1;
                    }
                    if (FileSec == 1) // use address in file? yes-use, no-ignore
                        addr = (ushort)(lo | (hi << 8)); // use address specified in file
                    var op = FileVerify ? "VERIFY" : "LOAD";
                    System.Diagnostics.Debug.WriteLine($"{op}@{addr:X4}");
                    int i;
                    while (success)
                    {
                        i = stream.ReadByte();
                        if (i >= 0 && i <= 255)
                        {
                            if (FileVerify)
                            {
                                if (memory[addr++] != (byte)i)
                                {
                                    err = 28; // VERIFY
                                    success = false;
                                }
                            }
                            else
                                memory[addr++] = (byte)i;
                        }
                        else
                            break; // end of file
                    }
                    stream.Close();
                }
                System.Diagnostics.Debug.WriteLine($"END {addr:X4}");
            }
            catch (FileNotFoundException)
            {
                err = 4; // FILE NOT FOUND
                success = false;
            }
            catch (Exception)
            {
                err = 1; // UNKNOWN - TOO MANY FILES
                success = false;
            }
            FileAddr = addr;
            return success;
        }

        protected bool FileSave(string filename, ushort addr1, ushort addr2)
        {
            try
            {
                if (!filename.ToLower().EndsWith(".prg"))
                    filename += ".prg";
                using (FileStream stream = File.OpenWrite(filename))
                {
                    stream.WriteByte(LO(addr1));
                    stream.WriteByte(HI(addr1));
                    for (ushort addr = addr1; addr <= addr2; ++addr)
                        stream.WriteByte(memory[addr]);
                    stream.Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
