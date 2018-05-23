using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch.Emulator8086.InternalDevices
{
  // see https://wiki.osdev.org/PIC
  public class ProgrammableInterruptController8259 : IInternalDevice
  {
    private readonly byte[] priorities = { 0, 1, 8, 9, 10, 11, 12, 13, 14, 15, 3, 4, 5, 6, 7 };
    private bool autoEoi;
    private bool icw4Needed;
    private int icwIndex;
    private int inServiceRegister;
    private int maskedIrqs; // masked = disabled IRQs
    private int requestRegister;
    private bool returnInServiceRegister;
    private byte vectorOffset = 8;

    IEnumerable<int> IInternalDevice.PortNumbers => Enumerable.Range(0x20, 2).Concat(Enumerable.Range(0xA0, 2));

    public (InterruptVector? interrupt, Action eoi) GetIrq()
    {
      for (var i = 0; i < priorities.Length; i++)
      {
        var irq = priorities[i];
        var irqMask = 1 << irq;
        if ((requestRegister & irqMask) != 0)
        {
          requestRegister &= ~irqMask;
          inServiceRegister |= irqMask;
          return ((InterruptVector)(vectorOffset + irq), () =>
          {
            if (autoEoi) { inServiceRegister &= ~irqMask; }
          });
        }
      }

      return (null, null);
    }

    public void Invoke(Irq irq)
    {
      var irqMask = 1 << (byte)irq;
      if ((maskedIrqs & irqMask) == 0)
      {
        requestRegister |= irqMask;
      }
    }

    byte IInternalDevice.GetByte(int port)
    {
      var result = 0;
      if (port == 0x20 || port == 0xA0)
      {
        result = returnInServiceRegister ? inServiceRegister : requestRegister;
      }
      else if (port == 0x21 || port == 0xA1)
      {
        result = maskedIrqs; // OCW1
      }

      if (port == 0xA0 || port == 0xA1)
      {
        result >>= 8;
      }

      return (byte)result;
    }

    void IInternalDevice.SetByte(int port, byte value)
    {
      if (port == 0x20 || port == 0xA0)
      {
        //if (port == 0xA0 && xt)
        //{
        //  maskedIrqs = 0xFF00 | (byte)maskedIrqs;
        //}

        if ((value & 0x10) != 0) // ICW1 marker
        {
          icw4Needed = (value & 0x01) != 0;
          icwIndex = 2;
        }
        else if ((value & 0x18) == 0) // OCW2
        {
          if (value == 0x20) // nonspecific EOI
          {
            inServiceRegister = 0;
          }
          else if (value >> 5 == 0b011) // specific EOI
          {
            var irq = value & 0b111;
            if (port == 0xA0) { irq += 8; }
            inServiceRegister &= ~(1 << irq);
          }
        }
        else if ((value & 0x98) == 0x08) // OCW3
        {
          returnInServiceRegister = (value & 0b11) == 0b11;
        }
      }
      else if (port == 0x21 || port == 0xA1)
      {
        if (icwIndex == 2)
        {
          vectorOffset = (byte)(value & 0b1111_1000);
          icwIndex = 4;
        }
        else if (icwIndex == 3) // only needed for cascade mode, i.e. two PICs
        {
          icwIndex++;
        }
        else if (icwIndex == 4)
        {
          autoEoi = (value & 010) != 0;
          icwIndex = 0;
        }
        else
        {
          maskedIrqs = value;
        }

        if (icwIndex == 4 && !icw4Needed) { icwIndex = 0; }
      }
    }
  }
}