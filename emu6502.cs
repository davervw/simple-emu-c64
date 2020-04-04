// emu6502.cs - class Emu6502 - MOS6502 Emulator
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
using System.Text;
using System.Collections.Generic;

namespace simple_emu_c64
{
    public class Emu6502
    {
        public interface Memory
        {
            byte this[ushort index]
            {
                get;
                set;
            }
        }

        public Memory memory;
        public HashSet<int> Breakpoints = new HashSet<int>();

        protected byte A = 0;
        protected byte X = 0;
        protected byte Y = 0;
        protected byte S = 0xFF;
        protected bool N = false;
        protected bool V = false;
        protected bool B = false;
        protected bool D = false;
        protected bool I = false;
        protected bool Z = false;
        protected bool C = false;
        protected ushort PC = 0;

        protected bool trace = false;
        protected bool step = false;

        public Emu6502(Memory memory)
        {
            this.memory = memory;
        }

        public void ResetRun()
        {
            ushort addr = (ushort)((memory[0xFFFC] | (memory[0xFFFD] << 8))); // RESET vector
            Execute(addr);
        }

        public virtual void Walk()
        {
            Walk6502.Reset();
            ushort addr = (ushort)((memory[0xFFFC] | (memory[0xFFFD] << 8))); // RESET vector
            Walk6502.Walk(this, addr);
        }

        protected virtual bool ExecutePatch()
        {
            return false;
        }

        void Execute(ushort addr)
        {
            bool conditional;
            byte bytes;

            PC = addr;

            while (true)
            {
                while (true)
                {
                    bytes = 1;
                    bool breakpoint = false;
                    if (Breakpoints.Contains(PC))
                        breakpoint = true;
                    if (trace || breakpoint || step)
                    {
                        ushort addr2;
                        string line;
                        string dis = Disassemble(PC, out conditional, out bytes, out addr2, out line);
                        string state = GetDisplayState();
                        System.Diagnostics.Debug.WriteLine(string.Format("{0}{1}", line.PadRight(30), state));
                        //Console.WriteLine(string.Format("{0}{1}", line.PadRight(30), state));
                        if (step)
                            step = step; // user can put debug breakpoint here to allow stepping
                        if (breakpoint)
                            breakpoint = breakpoint; // user can put debug breakpoint here to allow break
                    }
                    if (!ExecutePatch()) // allow execute to be overriden at a specific address
                        break;
                }

                switch (memory[PC])
                {
                    case 0x00: BRK(out bytes); break;
                    case 0x01: ORA(GetIndX(PC, out bytes)); break;
                    case 0x05: ORA(GetZP(PC, out bytes)); break;
                    case 0x06: SetZP(ASL(GetZP(PC, out bytes)), PC, out bytes); break;
                    case 0x08: PHP(); break;
                    case 0x09: ORA(GetIM(PC, out bytes)); break;
                    case 0x0A: SetA(ASL(A)); break;
                    case 0x0D: ORA(GetABS(PC, out bytes)); break;
                    case 0x0E: SetABS(ASL(GetABS(PC, out bytes)), PC, out bytes); break;

                    case 0x10: BPL(ref PC, out conditional, out bytes); break;
                    case 0x11: ORA(GetIndY(PC, out bytes)); break;
                    case 0x15: ORA(GetZPX(PC, out bytes)); break;
                    case 0x16: SetZPX(ASL(GetZPX(PC, out bytes)), PC, out bytes); break;
                    case 0x18: CLC(); break;
                    case 0x19: ORA(GetABSY(PC, out bytes)); break;
                    case 0x1D: ORA(GetABSX(PC, out bytes)); break;
                    case 0x1E: SetABSX(ASL(GetABSX(PC, out bytes)), PC, out bytes); break;

                    case 0x20: JSR(ref PC, out bytes); break;
                    case 0x21: AND(GetIndX(PC, out bytes)); break;
                    case 0x24: BIT(GetZP(PC, out bytes)); break;
                    case 0x25: AND(GetZP(PC, out bytes)); break;
                    case 0x26: SetZP(ROL(GetZP(PC, out bytes)), PC, out bytes); break;
                    case 0x28: PLP(); break;
                    case 0x29: AND(GetIM(PC, out bytes)); break;
                    case 0x2A: SetA(ROL(A)); break;
                    case 0x2C: BIT(GetABS(PC, out bytes)); break;
                    case 0x2D: AND(GetABS(PC, out bytes)); break;
                    case 0x2E: ROL(GetABS(PC, out bytes)); break;

                    case 0x30: BMI(ref PC, out conditional, out bytes); break;
                    case 0x31: AND(GetIndY(PC, out bytes)); break;
                    case 0x35: AND(GetZPX(PC, out bytes)); break;
                    case 0x36: SetZPX(ROL(GetZPX(PC, out bytes)), PC, out bytes); break;
                    case 0x38: SEC(); break;
                    case 0x39: AND(GetABSY(PC, out bytes)); break;
                    case 0x3D: AND(GetABSX(PC, out bytes)); break;
                    case 0x3E: SetABSX(ROL(GetABSX(PC, out bytes)), PC, out bytes); break;

                    case 0x40: RTI(ref PC, out bytes); break;
                    case 0x41: EOR(GetIndX(PC, out bytes)); break;
                    case 0x45: EOR(GetZP(PC, out bytes)); break;
                    case 0x46: SetZP(LSR(GetZP(PC, out bytes)), PC, out bytes); break;
                    case 0x48: PHA(); break;
                    case 0x49: EOR(GetIM(PC, out bytes)); break;
                    case 0x4A: SetA(LSR(A)); break;
                    case 0x4C: JMP(ref PC, out bytes); break;
                    case 0x4D: EOR(GetABS(PC, out bytes)); break;
                    case 0x4E: LSR(GetABS(PC, out bytes)); break;

                    case 0x50: BVC(ref PC, out conditional, out bytes); break;
                    case 0x51: EOR(GetIndY(PC, out bytes)); break;
                    case 0x55: EOR(GetZPX(PC, out bytes)); break;
                    case 0x56: SetZPX(LSR(GetZPX(PC, out bytes)), PC, out bytes); break;
                    case 0x58: CLI(); break;
                    case 0x59: EOR(GetABSY(PC, out bytes)); break;
                    case 0x5D: EOR(GetABSX(PC, out bytes)); break;
                    case 0x5E: SetABSX(LSR(GetABSX(PC, out bytes)), PC, out bytes); break;

                    case 0x60: RTS(ref PC, out bytes); break;
                    case 0x61: ADC(GetIndX(PC, out bytes)); break;
                    case 0x65: ADC(GetZP(PC, out bytes)); break;
                    case 0x66: SetZP(ROR(GetZP(PC, out bytes)), PC, out bytes); break;
                    case 0x68: PLA(); break;
                    case 0x69: ADC(GetIM(PC, out bytes)); break;
                    case 0x6A: SetA(ROR(A)); break;
                    case 0x6C: JMPIND(ref PC, out bytes); break;
                    case 0x6D: ADC(GetABS(PC, out bytes)); break;
                    case 0x6E: SetABS(ROR(GetABS(PC, out bytes)), PC, out bytes); break;

                    case 0x70: BVS(ref PC, out conditional, out bytes); break;
                    case 0x71: ADC(GetIndY(PC, out bytes)); break;
                    case 0x75: ADC(GetZPX(PC, out bytes)); break;
                    case 0x76: SetZPX(ROR(GetZPX(PC, out bytes)), PC, out bytes); break;
                    case 0x78: SEI(); break;
                    case 0x79: ADC(GetABSY(PC, out bytes)); break;
                    case 0x7D: ADC(GetABSX(PC, out bytes)); break;
                    case 0x7E: SetABSX(ROR(GetABSX(PC, out bytes)), PC, out bytes); break;

                    case 0x81: SetIndX(A, PC, out bytes); break;
                    case 0x84: SetZP(Y, PC, out bytes); break;
                    case 0x85: SetZP(A, PC, out bytes); break;
                    case 0x86: SetZP(X, PC, out bytes); break;
                    case 0x88: DEY(); break;
                    case 0x8A: TXA(); break;
                    case 0x8C: SetABS(Y, PC, out bytes); break;
                    case 0x8D: SetABS(A, PC, out bytes); break;
                    case 0x8E: SetABS(X, PC, out bytes); break;

                    case 0x90: BCC(ref PC, out conditional, out bytes); break;
                    case 0x91: SetIndY(A, PC, out bytes); break;
                    case 0x94: SetZPX(Y, PC, out bytes); break;
                    case 0x95: SetZPX(A, PC, out bytes); break;
                    case 0x96: SetZPY(X, PC, out bytes); break;
                    case 0x98: TYA(); break;
                    case 0x99: SetABSY(A, PC, out bytes); break;
                    case 0x9A: TXS(); break;
                    case 0x9D: SetABSX(A, PC, out bytes); break;

                    case 0xA0: SetY(GetIM(PC, out bytes)); break;
                    case 0xA1: SetA(GetIndX(PC, out bytes)); break;
                    case 0xA2: SetX(GetIM(PC, out bytes)); break;
                    case 0xA4: SetY(GetZP(PC, out bytes)); break;
                    case 0xA5: SetA(GetZP(PC, out bytes)); break;
                    case 0xA6: SetX(GetZP(PC, out bytes)); break;
                    case 0xA8: TAY(); break;
                    case 0xA9: SetA(GetIM(PC, out bytes)); break;
                    case 0xAA: TAX(); break;
                    case 0xAC: SetY(GetABS(PC, out bytes)); break;
                    case 0xAD: SetA(GetABS(PC, out bytes)); break;
                    case 0xAE: SetX(GetABS(PC, out bytes)); break;

                    case 0xB0: BCS(ref PC, out conditional, out bytes); break;
                    case 0xB1: SetA(GetIndY(PC, out bytes)); break;
                    case 0xB4: SetY(GetZPX(PC, out bytes)); break;
                    case 0xB5: SetA(GetZPX(PC, out bytes)); break;
                    case 0xB6: SetX(GetZPY(PC, out bytes)); break;
                    case 0xB8: CLV(); break;
                    case 0xB9: SetA(GetABSY(PC, out bytes)); break;
                    case 0xBA: TSX(); break;
                    case 0xBC: SetY(GetABSX(PC, out bytes)); break;
                    case 0xBD: SetA(GetABSX(PC, out bytes)); break;
                    case 0xBE: SetX(GetABSY(PC, out bytes)); break;

                    case 0xC0: CPY(GetIM(PC, out bytes)); break;
                    case 0xC1: CMP(GetIndX(PC, out bytes)); break;
                    case 0xC4: CPY(GetZP(PC, out bytes)); break;
                    case 0xC5: CMP(GetZP(PC, out bytes)); break;
                    case 0xC6: SetZP(DEC(GetZP(PC, out bytes)), PC, out bytes); break;
                    case 0xC8: INY(); break;
                    case 0xC9: CMP(GetIM(PC, out bytes)); break;
                    case 0xCA: DEX(); break;
                    case 0xCC: CPY(GetABS(PC, out bytes)); break;
                    case 0xCD: CMP(GetABS(PC, out bytes)); break;
                    case 0xCE: SetABS(DEC(GetABS(PC, out bytes)), PC, out bytes); break;

                    case 0xD0: BNE(ref PC, out conditional, out bytes); break;
                    case 0xD1: CMP(GetIndY(PC, out bytes)); break;
                    case 0xD5: CMP(GetZPX(PC, out bytes)); break;
                    case 0xD6: SetZPX(DEC(GetZPX(PC, out bytes)), PC, out bytes); break;
                    case 0xD8: CLD(); break;
                    case 0xD9: CMP(GetABSY(PC, out bytes)); break;
                    case 0xDD: CMP(GetABSX(PC, out bytes)); break;
                    case 0xDE: SetABSX(DEC(GetABSX(PC, out bytes)), PC, out bytes); break;

                    case 0xE0: CPX(GetIM(PC, out bytes)); break;
                    case 0xE1: SBC(GetIndX(PC, out bytes)); break;
                    case 0xE4: CPX(GetZP(PC, out bytes)); break;
                    case 0xE5: SBC(GetZP(PC, out bytes)); break;
                    case 0xE6: SetZP(INC(GetZP(PC, out bytes)), PC, out bytes); break;
                    case 0xE8: INX(); break;
                    case 0xE9: SBC(GetIM(PC, out bytes)); break;
                    case 0xEA: NOP(); break;
                    case 0xEC: CPX(GetABS(PC, out bytes)); break;
                    case 0xED: SBC(GetABS(PC, out bytes)); break;
                    case 0xEE: SetABS(INC(GetABS(PC, out bytes)), PC, out bytes); break;

                    case 0xF0: BEQ(ref PC, out conditional, out bytes); break;
                    case 0xF1: SBC(GetIndY(PC, out bytes)); break;
                    case 0xF5: SBC(GetZPX(PC, out bytes)); break;
                    case 0xF6: SetZPX(INC(GetZPX(PC, out bytes)), PC, out bytes); break;
                    case 0xF8: SED(); break;
                    case 0xF9: SBC(GetABSY(PC, out bytes)); break;
                    case 0xFD: SBC(GetABSX(PC, out bytes)); break;
                    case 0xFE: SetABSX(INC(GetABSX(PC, out bytes)), PC, out bytes); break;

                    default:
                        throw new Exception(string.Format("Invalid opcode {0:X2}", memory[PC]));
                }

                PC += bytes;
            }
        }

        void BRK(out byte bytes)
        {
            ++PC;
            PHP();
            Push(HI(PC));
            Push(LO(PC));
            B = true;
            PC = (ushort)(memory[0xFFFE] + (memory[0xFFFF] << 8));
            bytes = 0;
        }

        void CMP(byte value)
        {
            Subtract(A, value);
        }

        void CPX(byte value)
        {
            Subtract(X, value);
        }

        void CPY(byte value)
        {
            Subtract(Y, value);
        }

        void SBC(byte value)
        {
            if (D)
            {
                int A_dec = (A & 0xF) + ((A >> 4) * 10);
                int value_dec = (value & 0xF) + ((value >> 4) * 10);
                int result_dec = A_dec - value_dec - (C ? 0 : 1);
                C = (result_dec >= 0);
                if (!C)
                    result_dec = -result_dec; // absolute value
                int result = (result_dec % 10) | (((result_dec / 10) % 10) << 4);
                SetA(result);
                N = false; // undefined?
                V = false; // undefined?
            }
            else
            {
                byte result = Subtract(A, value, out V);
                SetA(result);
            }
        }

        byte Subtract(byte reg, byte value)
        {
            C = true; // init for CMP, etc.
            bool unused;
            return Subtract(reg, value, out unused);
        }

        byte Subtract(byte reg, byte value, out bool overflow)
        {
            bool old_reg_neg = (reg & 0x80) != 0;
            bool value_neg = (value & 0x80) != 0;
            int result = reg - value - (C ? 0 : 1);
            N = (result & 0x80) != 0;
            C = (result >= 0);
            Z = (result == 0);
            bool result_neg = (result & 0x80) != 0;
            overflow = (old_reg_neg && !value_neg && !result_neg) // neg - pos = pos
                || (!old_reg_neg && value_neg && result_neg); // pos - neg = neg
            return (byte)result;
        }

        void ADC(byte value)
        {
            int result;
            if (D)
            {
                int A_dec = (A & 0xF) + ((A >> 4) * 10);
                int value_dec = (value & 0xF) + ((value >> 4) * 10);
                int result_dec = A_dec + value_dec + (C ? 1 : 0);
                C = (result_dec > 99);
                result = (result_dec % 10) | (((result_dec / 10) % 10) << 4);
                SetA(result);
                Z = (result_dec == 0); // BCD quirk -- 100 doesn't set Z
                V = false;
            }
            else
            {
                bool A_old_neg = (A & 0x80) != 0;
                bool value_neg = (value & 0x80) != 0;
                result = A + value + (C ? 1 : 0);
                C = (result & 0x100) != 0;
                SetA(result);
                bool result_neg = (result & 0x80) != 0;
                V = (!A_old_neg && !value_neg && result_neg) // pos + pos = neg: overflow
                 || (A_old_neg && value_neg && !result_neg); // neg + neg = pos: overflow
            }
        }

        void ORA(int value)
        {
            SetA(A | value);
        }

        void EOR(int value)
        {
            SetA(A ^ value);
        }

        void AND(int value)
        {
            SetA(A & value);
        }

        void BIT(byte value)
        {
            Z = (A & value) == 0;
            N = (value & 0x80) != 0;
            V = (value & 0x40) != 0;
        }

        byte ASL(int value)
        {
            C = (value & 0x80) != 0;
            value = (byte)(value << 1);
            Z = (value == 0);
            N = (value & 0x80) != 0;
            return (byte)value;
        }

        byte LSR(int value)
        {
            C = (value & 0x01) != 0;
            value = (byte)(value >> 1);
            Z = (value == 0);
            N = false;
            return (byte)value;
        }

        byte ROL(int value)
        {
            bool newC = (value & 0x80) != 0;
            value = (byte)((value << 1) | (C ? 1 : 0));
            C = newC;
            Z = (value == 0);
            N = (value & 0x80) != 0;
            return (byte)value;
        }

        byte ROR(int value)
        {
            bool newC = (value & 0x01) != 0;
            N = C;
            value = (byte)((value >> 1) | (C ? 0x80 : 0));
            C = newC;
            Z = (value == 0);
            return (byte)value;
        }

        void Push(int value)
        {
            memory[(ushort)(0x100 + (S--))] = (byte)value;
        }

        protected byte Pop()
        {
            return memory[(ushort)(0x100 + (++S))];
        }

        void PHP()
        {
            int flags = (N ? 0x80 : 0)
                        | (V ? 0x40 : 0)
                        | (B ? 0x10 : 0)
                        | (D ? 0x08 : 0)
                        | (I ? 0x04 : 0)
                        | (Z ? 0x02 : 0)
                        | (C ? 0x01 : 0);
            Push(flags);
        }

        void PLP()
        {
            int flags = Pop();
            N = (flags & 0x80) != 0;
            V = (flags & 0x40) != 0;
            B = (flags & 0x10) != 0;
            D = (flags & 0x08) != 0;
            I = (flags & 0x04) != 0;
            Z = (flags & 0x02) != 0;
            C = (flags & 0x01) != 0;
        }

        void PHA()
        {
            Push(A);
        }

        void PLA()
        {
            SetA(Pop());
        }

        void CLC()
        {
            C = false;
        }

        void CLD()
        {
            D = false;
        }

        void CLI()
        {
            I = false;
        }

        void CLV()
        {
            V = false;
        }

        void SEC()
        {
            C = true;
        }

        void SED()
        {
            D = true;
        }

        void SEI()
        {
            I = true;
        }

        void INX()
        {
            X = INC(X);
        }

        void INY()
        {
            Y = INC(Y);
        }

        void DEX()
        {
            X = DEC(X);
        }

        void DEY()
        {
            Y = DEC(Y);
        }

        void NOP()
        {
        }

        byte DEC(byte value)
        {
            --value;
            Z = (value == 0);
            N = (value & 0x80) != 0;
            return (byte)value;
        }

        byte INC(byte value)
        {
            ++value;
            Z = (value == 0);
            N = (value & 0x80) != 0;
            return (byte)value;
        }

        void TXA()
        {
            SetReg(ref A, X);
        }

        void TAX()
        {
            SetReg(ref X, A);
        }

        void TYA()
        {
            SetReg(ref A, Y);
        }

        void TAY()
        {
            SetReg(ref Y, A);
        }

        void TXS()
        {
            S = X;
        }

        void TSX()
        {
            SetReg(ref X, S);
        }

        void BR(bool branch, ref ushort addr, out bool conditional, out byte bytes)
        {
            ushort addr2 = GetBR(addr, out conditional, out bytes);
            if (branch)
            {
                addr = addr2;
                bytes = 0; // don't advance addr
            }
        }

        void BPL(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(!N, ref addr, out conditional, out bytes);
        }

        void BMI(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(N, ref addr, out conditional, out bytes);
        }

        void BCC(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(!C, ref addr, out conditional, out bytes);
        }

        void BCS(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(C, ref addr, out conditional, out bytes);
        }

        void BVC(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(!V, ref addr, out conditional, out bytes);
        }

        void BVS(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(V, ref addr, out conditional, out bytes);
        }

        void BNE(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(!Z, ref addr, out conditional, out bytes);
        }

        void BEQ(ref ushort addr, out bool conditional, out byte bytes)
        {
            BR(Z, ref addr, out conditional, out bytes);
        }

        void JSR(ref ushort addr, out byte bytes)
        {
            bytes = 3; // for next calculation
            ushort addr2 = (ushort)(addr + bytes - 1);
            ushort addr3 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            Push(HI(addr2));
            Push(LO(addr2));
            addr = addr3;
            bytes = 0; // addr already changed
        }

        void RTS(ref ushort addr, out byte bytes)
        {
            byte lo = Pop();
            byte hi = Pop();
            bytes = 1; // make sure caller increases addr by one
            addr = (ushort)((hi << 8) | lo);
        }

        void RTI(ref ushort addr, out byte bytes)
        {
            PLP();
            byte hi = Pop();
            byte lo = Pop();
            bytes = 0; // make sure caller does not increase addr by one
            addr = (ushort)((hi << 8) | lo);
        }

        void JMP(ref ushort addr, out byte bytes)
        {
            bytes = 0; // caller should not advance address
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            addr = addr2;
        }

        void JMPIND(ref ushort addr, out byte bytes)
        {
            bytes = 0; // caller should not advance address
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            ushort addr3;
            if ((addr2 & 0xFF) == 0xFF) // JMP($XXFF) won't go over page boundary
                addr3 = (ushort)(memory[addr2] | (memory[(ushort)(addr2 - 0xFF)] << 8)); // 6502 "bug" - will use XXFF and XX00 as source of address
            else
                addr3 = (ushort)(memory[addr2] | (memory[(ushort)(addr2 + 1)] << 8));
            addr = addr3;
        }

        void SetA(int value)
        {
            SetReg(ref A, value);
        }

        void SetX(int value)
        {
            SetReg(ref X, value);
        }

        void SetY(int value)
        {
            SetReg(ref Y, value);
        }

        void SetReg(ref byte reg, int value)
        {
            reg = (byte)value;
            Z = (reg == 0);
            N = ((reg & 0x80) != 0);
        }

        byte GetIndX(ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] + X);
            return memory[(ushort)(memory[addr2] | (memory[(ushort)(addr2 + 1)] << 8))];
        }

        void SetIndX(byte value, ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] + X);
            ushort addr3 = (ushort)(memory[addr2] | (memory[(ushort)(addr2 + 1)] << 8));
            memory[addr3] = value;
        }

        byte GetIndY(ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)]);
            ushort addr3 = (ushort)((memory[addr2] | (memory[(ushort)(addr2 + 1)] << 8)) + Y);
            return memory[addr3];
        }

        void SetIndY(byte value, ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)]);
            ushort addr3 = (ushort)((memory[addr2] | (memory[(ushort)(addr2 + 1)] << 8)) + Y);
            memory[addr3]=value;
        }

        byte GetZP(ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = memory[(ushort)(addr + 1)];
            return memory[addr2];
        }

        void SetZP(byte value, ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = memory[(ushort)(addr + 1)];
            memory[addr2]=value;
        }

        byte GetZPX(ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = memory[(ushort)(addr + 1)];
            return memory[(byte)(addr2 + X)];
        }

        void SetZPX(byte value, ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = memory[(ushort)(addr + 1)];
            memory[(byte)(addr2 + X)] = value;
        }

        byte GetZPY(ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = memory[(ushort)(addr + 1)];
            return memory[(byte)(addr2 + Y)];
        }

        void SetZPY(byte value, ushort addr, out byte bytes)
        {
            bytes = 2;
            ushort addr2 = memory[(ushort)(addr + 1)];
            memory[(byte)(addr2 + Y)] = value;
        }

        byte GetABS(ushort addr, out byte bytes)
        {
            bytes = 3;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            return memory[addr2];
        }

        void SetABS(byte value, ushort addr, out byte bytes)
        {
            bytes = 3;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            memory[addr2] = value;
        }

        byte GetABSX(ushort addr, out byte bytes)
        {
            bytes = 3;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            return memory[(ushort)(addr2 + X)];
        }

        void SetABSX(byte value, ushort addr, out byte bytes)
        {
            bytes = 3;
            ushort addr2 = (ushort)((memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8)) + X);
            memory[addr2] = value;
        }

        byte GetABSY(ushort addr, out byte bytes)
        {
            bytes = 3;
            ushort addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            return memory[(ushort)(addr2 + Y)];
        }

        void SetABSY(byte value, ushort addr, out byte bytes)
        {
            bytes = 3;
            ushort addr2 = (ushort)((memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8)) + Y);
            memory [addr2] = value;
        }

        byte GetIM(ushort addr, out byte bytes)
        {
            bytes = 2;
            return memory[(ushort)(addr + 1)];
        }

        ushort GetBR(ushort addr, out bool conditional, out byte bytes)
        {
            conditional = true;
            bytes = 2;
            sbyte offset = (sbyte)memory[(ushort)(addr + 1)];
            ushort addr2 = (ushort)(addr + 2 + offset);
            return addr2;
        }

        byte LO(ushort value)
        {
            return (byte)value;
        }

        byte HI(ushort value)
        {
            return (byte)(value >> 8);
        }

        string GetDisplayState()
        {
            return string.Format("A:{0:X2} X:{1:X2} Y:{2:X2} S:{3:X2} P:{4}{5}-{6}{7}{8}{9}{10}",
                A,
                X,
                Y,
                S,
                N ? 'N' : ' ',
                V ? 'V' : ' ',
                B ? 'B' : ' ',
                D ? 'D' : ' ',
                I ? 'I' : ' ',
                Z ? 'Z' : ' ',
                C ? 'C' : ' '
                );
        }

        public string Disassemble(ushort addr, out bool conditional, out byte bytes, out ushort addr2, out string line)
        {
            string dis = Disassemble(addr, out conditional, out bytes, out addr2);
            StringBuilder s = new StringBuilder();
            s.AppendFormat("{0:X4} ", addr);
            for (int i = 0; i < 3; ++i)
            {
                if (i < bytes)
                    s.AppendFormat("{0:X2} ", memory[(ushort)(addr + i)]);
                else
                    s.Append("   ");
            }
            s.Append(dis);
            line = s.ToString();
            return dis;
        }

        string Disassemble(ushort addr, out bool conditional, out byte bytes, out ushort addr2)
        {
            conditional = false;
            addr2 = 0;
            bytes = 1;

            switch (memory[addr])
            {
                case 0x00: return "BRK";
                case 0x01: return IndX("ORA", addr, out bytes);
                case 0x05: return ZP("ORA", addr, out bytes);
                case 0x06: return ZP("ASL", addr, out bytes);
                case 0x08: return "PHP";
                case 0x09: return IM("ORA", addr, out bytes);
                case 0x0A: return "ASL A";
                case 0x0D: return ABS("ORA", addr, out bytes);
                case 0x0E: return ABS("ASL", addr, out bytes);

                case 0x10: return BR("BPL", addr, out conditional, out addr2, out bytes);
                case 0x11: return IndY("ORA", addr, out bytes);
                case 0x15: return ZPX("ORA", addr, out bytes);
                case 0x16: return ZPX("ASL", addr, out bytes);
                case 0x18: return "CLC";
                case 0x19: return ABSY("ORA", addr, out bytes);
                case 0x1D: return ABSX("ORA", addr, out bytes);
                case 0x1E: return ABSX("ASL", addr, out bytes);

                case 0x20: return ABS("JSR", addr, out addr2, out bytes);
                case 0x21: return IndX("AND", addr, out bytes);
                case 0x24: return ZP("BIT", addr, out bytes);
                case 0x25: return ZP("AND", addr, out bytes);
                case 0x26: return ZP("ROL", addr, out bytes);
                case 0x28: return "PLP";
                case 0x29: return IM("AND", addr, out bytes);
                case 0x2A: return "ROL A";
                case 0x2C: return ABS("BIT", addr, out bytes);
                case 0x2D: return ABS("AND", addr, out bytes);
                case 0x2E: return ABS("ROL", addr, out bytes);

                case 0x30: return BR("BMI", addr, out conditional, out addr2, out bytes);
                case 0x31: return IndY("AND", addr, out bytes);
                case 0x35: return ZPX("AND", addr, out bytes);
                case 0x36: return ZPX("ROL", addr, out bytes);
                case 0x38: return "SEC";
                case 0x39: return ABSY("AND", addr, out bytes);
                case 0x3D: return ABSX("AND", addr, out bytes);
                case 0x3E: return ABSX("ROL", addr, out bytes);

                case 0x40: return "RTI";
                case 0x41: return IndX("EOR", addr, out bytes);
                case 0x45: return ZP("EOR", addr, out bytes);
                case 0x46: return ZP("LSR", addr, out bytes);
                case 0x48: return "PHA";
                case 0x49: return IM("EOR", addr, out bytes);
                case 0x4A: return "LSR A";
                case 0x4C: return ABS("JMP", addr, out addr2, out bytes);
                case 0x4D: return ABS("EOR", addr, out bytes);
                case 0x4E: return ABS("LSR", addr, out bytes);

                case 0x50: return BR("BVC", addr, out conditional, out addr2, out bytes);
                case 0x51: return IndY("EOR", addr, out bytes);
                case 0x55: return ZPX("EOR", addr, out bytes);
                case 0x56: return ZPX("LSR", addr, out bytes);
                case 0x58: return "CLI";
                case 0x59: return ABSY("EOR", addr, out bytes);
                case 0x5D: return ABSX("EOR", addr, out bytes);
                case 0x5E: return ABSX("LSR", addr, out bytes);

                case 0x60: return "RTS";
                case 0x61: return IndX("ADC", addr, out bytes);
                case 0x65: return ZP("ADC", addr, out bytes);
                case 0x66: return ZP("ROR", addr, out bytes);
                case 0x68: return "PLA";
                case 0x69: return IM("ADC", addr, out bytes);
                case 0x6A: return "ROR A";
                case 0x6C: return Ind("JMP", addr, out addr2, out bytes);
                case 0x6D: return ABS("ADC", addr, out bytes);
                case 0x6E: return ABS("ROR", addr, out bytes);

                case 0x70: return BR("BVS", addr, out conditional, out addr2, out bytes);
                case 0x71: return IndY("ADC", addr, out bytes);
                case 0x75: return ZPX("ADC", addr, out bytes);
                case 0x76: return ZPX("ROR", addr, out bytes);
                case 0x78: return "SEI";
                case 0x79: return ABSY("ADC", addr, out bytes);
                case 0x7D: return ABSX("ADC", addr, out bytes);
                case 0x7E: return ABSX("ROR", addr, out bytes);

                case 0x81: return IndX("STA", addr, out bytes);
                case 0x84: return ZP("STY", addr, out bytes);
                case 0x85: return ZP("STA", addr, out bytes);
                case 0x86: return ZP("STX", addr, out bytes);
                case 0x88: return "DEY";
                case 0x8A: return "TXA";
                case 0x8C: return ABS("STY", addr, out bytes);
                case 0x8D: return ABS("STA", addr, out bytes);
                case 0x8E: return ABS("STX", addr, out bytes);

                case 0x90: return BR("BCC", addr, out conditional, out addr2, out bytes);
                case 0x91: return IndY("STA", addr, out bytes);
                case 0x94: return ZPX("STY", addr, out bytes);
                case 0x95: return ZPX("STA", addr, out bytes);
                case 0x96: return ZPY("STX", addr, out bytes);
                case 0x98: return "TYA";
                case 0x99: return ABSY("STA", addr, out bytes);
                case 0x9A: return "TXS";
                case 0x9D: return ABSX("STA", addr, out bytes);

                case 0xA0: return IM("LDY", addr, out bytes);
                case 0xA1: return IndX("LDA", addr, out bytes);
                case 0xA2: return IM("LDX", addr, out bytes);
                case 0xA4: return ZP("LDY", addr, out bytes);
                case 0xA5: return ZP("LDA", addr, out bytes);
                case 0xA6: return ZP("LDX", addr, out bytes);
                case 0xA8: return "TAY";
                case 0xA9: return IM("LDA", addr, out bytes);
                case 0xAA: return "TAX";
                case 0xAC: return ABS("LDY", addr, out bytes);
                case 0xAD: return ABS("LDA", addr, out bytes);
                case 0xAE: return ABS("LDX", addr, out bytes);

                case 0xB0: return BR("BCS", addr, out conditional, out addr2, out bytes);
                case 0xB1: return IndY("LDA", addr, out bytes);
                case 0xB4: return ZPX("LDY", addr, out bytes);
                case 0xB5: return ZPX("LDA", addr, out bytes);
                case 0xB6: return ZPY("LDX", addr, out bytes);
                case 0xB8: return "CLV";
                case 0xB9: return ABSY("LDA", addr, out bytes);
                case 0xBA: return "TSX";
                case 0xBC: return ABSX("LDY", addr, out bytes);
                case 0xBD: return ABSX("LDA", addr, out bytes);
                case 0xBE: return ABSY("LDX", addr, out bytes);

                case 0xC0: return IM("CPY", addr, out bytes);
                case 0xC1: return IndX("CMP", addr, out bytes);
                case 0xC4: return ZP("CPY", addr, out bytes);
                case 0xC5: return ZP("CMP", addr, out bytes);
                case 0xC6: return ZP("DEC", addr, out bytes);
                case 0xC8: return "INY";
                case 0xC9: return IM("CMP", addr, out bytes);
                case 0xCA: return "DEX";
                case 0xCC: return ABS("CPY", addr, out bytes);
                case 0xCD: return ABS("CMP", addr, out bytes);
                case 0xCE: return ABS("DEC", addr, out bytes);

                case 0xD0: return BR("BNE", addr, out conditional, out addr2, out bytes);
                case 0xD1: return IndY("CMP", addr, out bytes);
                case 0xD5: return ZPX("CMP", addr, out bytes);
                case 0xD6: return ZPX("DEC", addr, out bytes);
                case 0xD8: return "CLD";
                case 0xD9: return ABSY("CMP", addr, out bytes);
                case 0xDD: return ABSX("CMP", addr, out bytes);
                case 0xDE: return ABSX("DEC", addr, out bytes);

                case 0xE0: return IM("CPX", addr, out bytes);
                case 0xE1: return IndX("SBC", addr, out bytes);
                case 0xE4: return ZP("CPX", addr, out bytes);
                case 0xE5: return ZP("SBC", addr, out bytes);
                case 0xE6: return ZP("INC", addr, out bytes);
                case 0xE8: return "INX";
                case 0xE9: return IM("SBC", addr, out bytes);
                case 0xEA: return "NOP";
                case 0xEC: return ABS("CPX", addr, out bytes);
                case 0xED: return ABS("SBC", addr, out bytes);
                case 0xEE: return ABS("INC", addr, out bytes);

                case 0xF0: return BR("BEQ", addr, out conditional, out addr2, out bytes);
                case 0xF1: return IndY("SBC", addr, out bytes);
                case 0xF5: return ZPX("SBC", addr, out bytes);
                case 0xF6: return ZPX("INC", addr, out bytes);
                case 0xF8: return "SED";
                case 0xF9: return ABSY("SBC", addr, out bytes);
                case 0xFD: return ABSX("SBC", addr, out bytes);
                case 0xFE: return ABSX("INC", addr, out bytes);

                default:
                    return "???";
                    //throw new Exception(string.Format("Invalid opcode {0:X2}", memory[addr]));
            }
        }

        string Ind(string opcode, ushort addr, out ushort addr2, out byte bytes)
        {
            bytes = 3;
            ushort addr1 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            addr2 = (ushort)(memory[addr1] | (memory[(ushort)(addr1 + 1)] << 8));
            return string.Format("{0} (${1:X4})", opcode, addr1);
        }

        string IndX(string opcode, ushort addr, out byte bytes)
        {
            bytes = 2;
            return string.Format("{0} (${1:X2},X)", opcode, memory[(ushort)(addr + 1)]);
        }

        string IndY(string opcode, ushort addr, out byte bytes)
        {
            bytes = 2;
            return string.Format("{0} (${1:X2}),Y", opcode, memory[(ushort)(addr + 1)]);
        }

        string ZP(string opcode, ushort addr, out byte bytes)
        {
            bytes = 2;
            return string.Format("{0} ${1:X2}", opcode, memory[(ushort)(addr + 1)]);
        }

        string ZPX(string opcode, ushort addr, out byte bytes)
        {
            bytes = 2;
            return string.Format("{0} ${1:X2},X", opcode, memory[(ushort)(addr + 1)]);
        }

        string ZPY(string opcode, ushort addr, out byte bytes)
        {
            bytes = 2;
            return string.Format("{0} ${1:X2},Y", opcode, memory[(ushort)(addr + 1)]);
        }

        string ABS(string opcode, ushort addr, out byte bytes)
        {
            bytes = 3;
            return string.Format("{0} ${1:X4}", opcode, memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
        }

        string ABS(string opcode, ushort addr, out ushort addr2, out byte bytes)
        {
            bytes = 3;
            addr2 = (ushort)(memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
            return string.Format("{0} ${1:X4}", opcode, addr2);
        }

        string ABSX(string opcode, ushort addr, out byte bytes)
        {
            bytes = 3;
            return string.Format("{0} ${1:X4},X", opcode, memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
        }

        string ABSY(string opcode, ushort addr, out byte bytes)
        {
            bytes = 3;
            return string.Format("{0} ${1:X4},Y", opcode, memory[(ushort)(addr + 1)] | (memory[(ushort)(addr + 2)] << 8));
        }

        string IM(string opcode, ushort addr, out byte bytes)
        {
            bytes = 2;
            return string.Format("{0} #${1:X2}", opcode, memory[(ushort)(addr + 1)]);
        }

        string BR(string opcode, ushort addr, out bool conditional, out ushort addr2, out byte bytes)
        {
            bytes = 2;
            conditional = true;
            sbyte offset = (sbyte)memory[(ushort)(addr + 1)];
            addr2 = (ushort)(addr + 2 + offset);
            return string.Format("{0} ${1:X4}", opcode, addr2);
        }
    }
}
