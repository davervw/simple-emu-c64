# simple-emu-c64 #
Here is a simple Commodore 64 and 6502 Emulator I wrote from scratch.  Runs in a text console window.

Notable features

* Emulates MOS6502
* Runs as a Windows Console Program (can probably be ported elsewhere pretty easily, current source is C#)
* Text based Commodore 64 BASIC and 6502 Assembly/Machine Code programs supported
* only a few hooks: CHRIN-$FFCF/CHROUT-$FFD2/COLOR-$D021/199/646

![Sample.bas](https://github.com/davervw/simple-emu-c64/raw/master/Sample.png)

LIMITATIONS:

* Only keyboard/console I/O.  No text pokes, no graphics.  Just stdio.  No backspace.  No asynchronous input (GET K$), but INPUT S$ works.  No special Commodore keys, e.g. function keys, cursor keys, color keys, etc.
* No keyboard color switching.  No border or border color.
* No screen editing (gasp!) Just short and sweet for running C64 BASIC in terminal/console window via 6502 chip emulation in software
* No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126)
* No memory management.  Not full 64K RAM despite startup screen.
   Just 44K RAM, 16K ROM, 1K VIC-II color RAM nybbles
* No timers.  No interrupts except BRK.  No NMI/RESTORE key.  No STOP key.
* No loading of files implemented.
* Where do I plug in my cartridge?  What, no joystick?
* Lightly tested.  Bugs are lurking! 

CREDITS:

* [Micro Logic Corp. 6502 (65XX) Microprocessor Instant Reference Card](https://archive.org/details/6502MicroprocessorInstantReferenceCard)
* Compute's [Mapping the Commodore 64](https://archive.org/details/Compute_s_Mapping_the_Commodore_64)
* [VICE](https://vice-emu.sourceforge.io/) for performing trace comparisons 
* Commodore 64 BASIC/KERNAL ROM from [VICE](https://vice-emu.sourceforge.io/) or [Zimmers.net](http://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/) or other
* Inspired by [mist64/cbmbasic](https://github.com/mist64/cbmbasic)
* Built with Microsoft Visual Studio 2017, .NET Framework 4, C#