# 8086 emulator

Primarily the CPU itself is implemented, supporting all 8086 opcodes.

Additionally implemented internal devices are:
- CRT controller 6845 supporting 80x25 16 color text mode,
- DMA controller 8237,
- Programmable Interrupt Timer 8253,
- Programmable Peripheral Interface 8255 (keyboard controller),
- Programmable Interrupt Controller 8259,
- CMOS real time clock.

The emulator can run the original IBM PC BIOS and the IBM BASIC operating system.

Used documentation:
- [Intel 8086 Family User's Manual October 1979](https://edge.edx.org/c4x/BITSPilani/EEE231/asset/8086_family_Users_Manual_1_.pdf)
- [OSDev.org](https://wiki.osdev.org/Main_Page)
- IBM PC BIOS listing (1982-10-27)
