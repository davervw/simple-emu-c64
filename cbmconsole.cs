// cbmconsole.cs - Class CBM_Console - Commodore Console Emulation
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

using System;
using System.Collections.Generic;

namespace simple_emu_c64
{
    public class CBM_Console
    {
        static bool supress_next_clear = true;
        static bool supress_next_home = false;
        static bool supress_next_cr = false;
        static List<char> buffer = new List<char>();

        public enum CBMEncoding
        {
            ascii = 0,
            //ansi = 1,
            //unicode = 2,
            petscii = 3,
        }

        private static CBMEncoding encoding;

        public static CBMEncoding Encoding
        {
            get
            {
                return encoding;
            }

            set
            {
                encoding = value;
                if (encoding == CBMEncoding.ascii)
                    Console.OutputEncoding = System.Text.Encoding.ASCII;
                else if (encoding == CBMEncoding.petscii)
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
        }

        public static void WriteChar(char c, bool supress_next_home=false)
        {
            // we're emulating, so draw character on local console window
            if (c == 0x0D || c == 0x8D)
            {
                if (supress_next_cr)
                    supress_next_cr = false;
                else
                    Console.WriteLine();
            }
            else if (c >= ' ' && c <= '~')
            {
                if (encoding == CBMEncoding.petscii)
                {
                    if (c == '\\')
                        c = '£';
                    else if (c == '^')
                        c = '↑';
                    else if (c == '_')
                        c = '←';
                    else if (c == '\x7E')
                        c = 'π';
                }
                //System.Diagnostics.Debug.WriteLine($"Printable {(ulong)c:X8}");
                Console.Write(c);
            }
            else if (c == 157) // left
            {
                if (Console.CursorLeft > 0)
                    Console.Write('\b');
                else if (Console.CursorTop > 0)
                {
                    --Console.CursorTop;
                    Console.CursorLeft = Console.BufferWidth - 1;
                }
            }
            else if (c == 29) // right
            {
                if (Console.CursorLeft < Console.BufferWidth - 1)
                    ++Console.CursorLeft;
                else
                    Console.WriteLine();
            }
            else if (c == 145) // up
            {
                if (Console.CursorTop > 0)
                    --Console.CursorTop;
            }
            else if (c == 17) // down
            {
                int left = Console.CursorLeft;
                Console.WriteLine();
                Console.CursorLeft = left;
            }
            else if (c == 19) // home
            {
                if (CBM_Console.supress_next_home) // use class static here, if arg set, will set for next time
                {
                    CBM_Console.supress_next_home = false;
                }
                else
                {
                    Console.CursorTop = 0;
                    Console.CursorLeft = 0;
                }
            }
            else if (c == 147)
            {
                try
                {
                    if (supress_next_clear)
                        supress_next_clear = false;
                    else
                        Console.Clear();
                }
                catch (Exception)
                {
                    // ignore exception, e.g. not a console
                }
            }
            else if (encoding == CBMEncoding.petscii && (c == 255 || c == 0xDE))
                Console.Write('π');
            else
            {
                if (encoding == CBMEncoding.petscii)
                {
                    if (c == '\xA0' || c == '\xE0') // alternate space
                        c = '\u00A0'; // no-break space
                    else if (c == '\xB0') // nw box line corner
                        c = '\u250c';
                    else if (c == '\xAE') // ne box line corner
                        c = '\u2510';
                    else if (c == '\xAD') // sw box line corner
                        c = '\u2514';
                    else if (c == '\xBD') // se box line corner
                        c = '\u2518';
                    else if (c == '\xAC' || c == '\xEC') // se graphic box
                        c = '\u2597';
                    else if (c == '\xBB' || c == '\xFB') // sw graphic box
                        c = '\u2596';
                    else if (c == '\xBC' || c == '\xFC') // ne graphic box
                        c = '\u259d';
                    else if (c == '\xBE' || c == '\xFE') // nw graphic box
                        c = '\u2598';
                    else if (c == '\xBF') // nwse diagonal graphic box
                        c = '\u259A';
                    else if (c == '\xA2' || c == '\xE2') // lower half graphic box
                        c = '\u2584';
                    else if (c == '\xA1' || c == '\xE1') // left half graphic box
                        c = '\u258C';
                    else
                        return;
                    Console.Write(c);
                }
                //System.Diagnostics.Debug.WriteLine(string.Format("Unprintable {0:X2}", (int)c));
            }

            if (supress_next_home)
                CBM_Console.supress_next_home = true;
        }

        // blocking read to get next typed character
        public static byte ReadChar()
        {
            if (buffer.Count == 0)
            {
                // System.Console.ReadLine() has features of history (cursor up/down, F7/F8), editing (cursor left/right, delete, backspace, etc.)
                buffer.AddRange(Console.ReadLine());
                buffer.Add('\r');
                //Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1); // Up one line
                supress_next_cr = true;
            }
            char c = buffer[0];
            if (encoding == CBMEncoding.petscii)
            {
                if (c == 'π')
                    c = '\xff';
                else if (c == '£')
                    c = '\\';
                else if (c == '↑' || c == '\u0018')
                    c = '^';
                else if (c == '←' || c == '\u001b')
                    c = '_';
                else if (c == '\u00A0') // no-break space
                    c = '\xA0'; // alternate space
                else if (c == '\u250c' || c == '\u250f')
                    c = '\xB0'; // nw box line corner
                else if (c == '\u2510' || c == '\u2513')
                    c = '\xAE'; // ne box line corner
                else if (c == '\u2514' || c == '\u2517')
                    c = '\xAD'; // sw box line corner
                else if (c == '\u2518' || c == '\u251b')
                    c = '\xBD'; // se box line corner
                //else if (c > '\xff')
                //    System.Diagnostics.Debug.WriteLine($"Received unicode {(ulong)c}");
            }
            buffer.RemoveAt(0);
            return (byte)c;
        }

        public static byte GetIn()
        {
            if (buffer.Count > 0)
                return ReadChar();
            else if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                return (byte)key.KeyChar;
            }
            else
                return 0;
        }

        public static bool CheckStop()
        {
            if (buffer.Count > 0)
            {
                if (buffer.Contains('\x1B'))
                    return true;
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == '\x1B') // ESC
                {
                    buffer.Clear();
                    return true;
                }
                else if (key.KeyChar != 0)
                {
                    Push(key.KeyChar.ToString());
                }
            }

            return false;
        }

        public static void Push(string s)
        {
            buffer.AddRange(s);
        }
    }
}
