// cbmconsole.cs - Class CBM_Console - Commodore Console Emulation
//
////////////////////////////////////////////////////////////////////////////////
//
// simple-emu-c64
// C64/6502 Emulator for Microsoft Windows Console
//
// MIT License
//
// Copyright (c) 2020 by David R. Van Wagner
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

        public static ApplyColorDelegate ApplyColor = null; // optionally apply color when displaying characters
        public delegate void ApplyColorDelegate();

        public static void WriteChar(char c, bool supress_next_home=false)
        {
            // we're emulating, so draw character on local console window
            if (c == 0x0D)
            {
                if (supress_next_cr)
                    supress_next_cr = false;
                else
                    Console.WriteLine();
            }
            else if (c >= ' ' && c <= '~')
            {
                ApplyColor?.Invoke();
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

            if (supress_next_home)
                CBM_Console.supress_next_home = true;
        }

        // blocking read to get next typed character
        public static byte ReadChar()
        {
            ////OLD VERSION - BACKSPACE DIDN'T WORK
            //while (true)
            //{
            //    if (Console.KeyAvailable) // Note: requires console
            //    {
            //        int i = Console.ReadKey(true).KeyChar; // Note: requires console
            //        if (i == '\b' || i == '\r' || (i >= ' ' && i <= '~'))
            //        {
            //            if (i != '\r')
            //                Console.Write((char)i);
            //            if (i == '\b')
            //                i = 20; // DEL -- NOTE: doesn't work
            //            return (byte)i;
            //        }
            //    }
            //    else
            //        System.Threading.Thread.Sleep(20); // be nice to CPU
            //}

            if (buffer.Count == 0)
            {
                // System.Console.ReadLine() has features of history (cursor up/down, F7/F8), editing (cursor left/right, delete, backspace, etc.)
                ApplyColor?.Invoke();
                buffer.AddRange(Console.ReadLine());
                buffer.Add('\r');
                //Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1); // Up one line
                supress_next_cr = true;
            }
            char c = buffer[0];
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
