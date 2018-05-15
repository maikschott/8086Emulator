# 8086 emulator

Primarily the CPU itself is implemented, supporting all 8086 opcodes.

Additionally implemented internal devices are the DMA controller 8237, Programmable Interrupt Controller 8259, Programmable Interrupt Timer 8253 and the CMOS real time clock. An IBM BIOS is runnable, but can currently not complete all of its POST checks.

80×25 monochrome text mode is supported by writing directly to the B0000 address block.

Used documentation:
- [Intel 8086 Family User's Manual October 1979](https://edge.edx.org/c4x/BITSPilani/EEE231/asset/8086_family_Users_Manual_1_.pdf)
- [OSDev.org](https://wiki.osdev.org/Main_Page)
