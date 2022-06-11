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
        static List<ConsoleKeyInfo> keybuffer = new List<ConsoleKeyInfo>();
        static bool stop_pressed = false;

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

        public static bool Color { get; set; }
        //Warning: Default light blue on blue doesn't look good with default color palette for Windows console
        //You can manually modify color pallette in Windows (see suggested colors documented in emuc64.cs)

        public static bool Lowercase { get; set; }

        public static bool Reverse { get; set; }

        public static bool QuoteMode { get; set; }

        public static bool InsertMode { get; set; }

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
            else if (encoding == CBMEncoding.petscii && (byte)c < 32 && (QuoteMode || InsertMode) && !((byte)c == 20 && !InsertMode))
            {
                Console.Write((char)(0xE200 | ((byte)c + (byte)'A' - 1) | (Lowercase ? 0x100 : 0)));
                return;
            }
            else if (encoding == CBMEncoding.petscii && (byte)c >= 128 && ((byte)c & 127) < 32 && (QuoteMode || InsertMode))
            {
                Console.Write((char)(0xE200 | (((byte)c & 127) + (byte)'a' - 1) | (Lowercase ? 0x100 : 0)));
                return;
            }
            else if (encoding == CBMEncoding.petscii && ((byte)c & 127) >= 32)
            {
                if (Reverse)
                {
                    Console.Write((char)(0xE200 | (byte)c | (Lowercase ? 0x100 : 0)));
                    return;
                }
                if (c == '\\')
                {
                    Console.Write('£');
                    return;
                }
                else if (c == '^')
                {
                    Console.Write('↑');
                    return;
                }
                else if (c == '_')
                {
                    Console.Write('←');
                    return;
                }
                else if (c == '\x7E')
                {
                    Console.Write('π');
                    return;
                }
                else if (c >= '\x20' && c < '\x40' || c == '[' || c == ']')
                {
                    Console.Write(c);
                    return;
                }
                else if (c >= 'A' && c <= 'Z' || Lowercase && c >= 'a' && c <= 'z')
                {
                    if (Lowercase)
                        Console.Write((char)((int)c ^ 0x20));
                    else
                        Console.Write(c);
                    return;
                }
                Console.Write((char)(0xE000 | (byte)c | (Lowercase ? 0x100 : 0)));
            }
            else if (c >= ' ' && c <= '~')
                Console.Write(c);
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
            buffer.RemoveAt(0);
            if (Lowercase && (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z'))
                c = (char)((byte)c ^ 0x20); // toggle case
            else if (c == '\b')
                c = '\x14';
            else if (encoding == CBMEncoding.petscii)
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
                else if (c == '\u2022')
                    c = '\xD1'; // bullet
                else if (c >= '\ue000' && c <= '\ue0ff')
                    c = (char)(byte)c;
                //else if (c > '\xff')
                //    System.Diagnostics.Debug.WriteLine($"Received unicode {(ulong)c}");
            }
            return (byte)c;
        }

        public static byte GetIn(bool check_stop = false)
        {
            if (buffer.Count == 0 || check_stop)
            {
                if (!Console.KeyAvailable && !check_stop && keybuffer.Count == 0)
                    return 0;

                ConsoleKeyInfo key;
                if (check_stop || keybuffer.Count == 0)
                {
                    key = Console.ReadKey(true);
                    if (check_stop)
                        keybuffer.Add(key); // assume must be buffered
                }
                else
                {
                    key = keybuffer[0];
                    keybuffer.RemoveAt(0);
                }
                                        
                //System.Diagnostics.Debug.WriteLine($"{key.Key} {key.Modifiers} {(int)key.KeyChar}");

                if (key.Key == ConsoleKey.LeftArrow)
                    return 157;
                if (key.Key == ConsoleKey.RightArrow)
                    return 29;
                if (key.Key == ConsoleKey.UpArrow)
                    return 145;
                if (key.Key == ConsoleKey.DownArrow)
                    return 17;
                if (key.Key == ConsoleKey.Home)
                {
                    if ((key.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                        return 147;
                    else
                        return 19;
                }
                if (key.Key == ConsoleKey.Delete)
                    return 20;
                if (key.KeyChar == 0)
                    return 0;
                if (key.KeyChar == 27)
                {
                    if (check_stop)
                        keybuffer.RemoveAt(keybuffer.Count - 1); // don't buffer STOP
                    stop_pressed = true;
                    return 0;
                }

                if (check_stop)
                    keybuffer.RemoveAt(keybuffer.Count - 1); // not buffered here
                Push(key.KeyChar.ToString());

                if (check_stop)
                    return 0;
            }

            return ReadChar();
        }

        public static bool CheckStop()
        {
            if (Console.KeyAvailable)
                CBM_Console.GetIn(check_stop: true);

            if (stop_pressed)
            {
                stop_pressed = false;
                buffer.Clear();
                return true;
            }

            return false;
        }

        public static void Push(string s)
        {
            buffer.AddRange(s);
        }
    }
}
