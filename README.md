# 8086 emulator

Primarily the CPU itself is implemented, supporting all 8086 and 80186 opcodes, including undocumented ones.

Additionally implemented internal devices are:
- CRT controller 6845 supporting 40×25, 80×25 monochrome and 16 colors text modes,
- DMA controller 8237,
- Programmable Interrupt Timer 8253,
- Programmable Peripheral Interface 8255 (keyboard controller),
- Programmable Interrupt Controller 8259,
- CMOS real time clock.

The emulator can run the original IBM PC BIOS and the IBM (Cassette) BASIC interpreter.

Used documentation:
- [Intel 8086 Family User's Manual October, 1979](https://edge.edx.org/c4x/BITSPilani/EEE231/asset/8086_family_Users_Manual_1_.pdf)
- Introduction to the 80186 Microprocessor (AP-186), 1983
- [OSDev.org](https://wiki.osdev.org/Main_Page)
- IBM PC BIOS (1982-10-27) listing

## Future work
- graphic modes,
- better keyboard handling,
- floppy/hard disk drive controller,
- IBM PC XT or AT compatibility,
- etc.