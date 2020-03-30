﻿// cbmconsole.cs - Class CBM_Console - Commodore Console Emulation
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
        static List<char> buffer = new List<char>();

        public static ApplyColorDelegate ApplyColor = null; // optionally apply color when displaying characters
        public delegate void ApplyColorDelegate();

        public static void WriteChar(char c)
        {
            // we're emulating, so draw character on local console window
            if (c == 0x0D)
                Console.WriteLine();
            else if (c >= ' ' && c <= '~')
            {
                ApplyColor?.Invoke();
                Console.Write(c);
            }
            else if (c == 147)
            {
                try
                {
                    Console.Clear();
                }
                catch (Exception)
                {
                    // ignore exception, e.g. not a console
                }
            }
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
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1); // Up one line
            }
            char c = buffer[0];
            buffer.RemoveAt(0);
            return (byte)c;
        }
    }
}
