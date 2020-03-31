# simple-emu-c64 #
Here is a simple Commodore 64 (and VIC-20, ...) and 6502 Emulator I wrote from scratch.  Runs in a text console window.

Notable features

* Emulates MOS6502
* Runs as a Windows Console Program (can probably be ported elsewhere pretty easily, current source is C#)
* Text based Commodore 64 BASIC and 6502 Assembly/Machine Code programs supported
* only a few hooks: CHRIN-$FFCF/CHROUT-$FFD2/COLOR-$D021/199/646 (COLOR background/inverse $9001 on VIC-20)

![Sample.bas](https://github.com/davervw/simple-emu-c64/raw/master/Sample.png)

USAGE:

    simple-emu-c64
    simple-emu-c64 c64
	simple-emu-c64 c64 walk
	simple-emu-c64 c64 walk FFD2
    simple-emu-c64 vic20
    simple-emu-c64 vic20 walk
    simple-emu-c64 vic20 walk FFD2 FFCF

LIMITATIONS:

* No keyboard color switching.  No border or border color.
* No screen editing (gasp!) Just short and sweet for running C64 BASIC in terminal/console window via 6502 chip emulation in software
* No PETSCII graphic characters, only supports printables CHR$(32) to CHR$(126).  But does support CHR$(147) for clear screen.
* No memory management or banking.  Not full 64K RAM despite what the startup screen says.
   Just 44K RAM, 16K ROM, 1K VIC-II color RAM nybbles
* No timers.  No interrupts except BRK.  No NMI/RESTORE key.  No STOP key.
* No loading or saving of files implemented (but Windows clipboard works!)
* Where do I plug in my cartridge?  What, no joystick?
* Lightly tested.  Bugs are lurking! 

CREDITS:

* [Micro Logic Corp. 6502 (65XX) Microprocessor Instant Reference Card](https://archive.org/details/6502MicroprocessorInstantReferenceCard)
* Compute's [Mapping the Commodore 64](https://archive.org/details/Compute_s_Mapping_the_Commodore_64)
* Compute's [Mapping the VIC](https://archive.org/details/COMPUTEs_Mapping_the_VIC_1984_COMPUTE_Publications)
* [VICE](https://vice-emu.sourceforge.io/) for performing trace comparisons 
* Commodore 64 and VIC-20 BASIC/KERNAL ROM from [VICE](https://vice-emu.sourceforge.io/) or [Zimmers.net](http://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/) or other
* Inspired by [mist64/cbmbasic](https://github.com/mist64/cbmbasic)
* Built with Microsoft Visual Studio 2017, .NET Framework 4, C#