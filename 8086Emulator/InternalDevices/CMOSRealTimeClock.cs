using System;
using System.Collections.Generic;
using System.Linq;

namespace Masch._8086Emulator.InternalDevices
{
  // ReSharper disable once InconsistentNaming
  public class CMOSRealTimeClock : IInternalDevice
  {
    private TimeSpan virtualTimeOffset;
    private int year, month, day;
    private int alarmHour, alarmMinute, alarmSecond;
    private byte index;
    private bool nmiDisabled;
    private bool isBcd;
    private bool hours24 = true;
    private readonly byte[] data;

    public IEnumerable<int> PortNumbers => Enumerable.Range(0x70, 2);

    public CMOSRealTimeClock()
    {     
      year = 1985; // it's back to the 80's
      month = DateTime.Today.Month;
      day = DateTime.Today.Day;
      virtualTimeOffset = TimeSpan.Zero;
      data = new byte[128];
    }

    public byte GetByte(int port)
    {
      if (port == 0x70)
      {
        return (byte)(index | (nmiDisabled ? 0x80 : 0x00));
      }
      if (port == 0x71)
      {
        var virtualTime = DateTime.Now + virtualTimeOffset;
        switch (index)
        {
          case 0x00:
            return ToBcd(virtualTime.Second);
          case 0x01:
            return ToBcd(alarmSecond);
          case 0x02:
            return ToBcd(virtualTime.Minute);
          case 0x03:
            return ToBcd(alarmMinute);
          case 0x04:
            return ToBcd(hours24 ? virtualTime.Hour : virtualTime.Hour % 12);
          case 0x05:
            return ToBcd(hours24 ? alarmHour : alarmHour % 12);
          case 0x06:
            return ToBcd((byte)new DateTime(year, month, day).DayOfWeek);
          case 0x07:
            return ToBcd(day);
          case 0x08:
            return ToBcd(month);
          case 0x09:
            return ToBcd(year - 1900);
          case 0x0B:
          {
            byte result = 0;
            if (hours24) { result |= 0x02; } 
            if (isBcd) { result |= 0x04; }
            return result;
          }
          case 0x0D:
            return 0x80; // Real-Time Clock has power
          case 0x14:
            return 0x30; // primary display: 0x00 = adapter card with option ROM, 0x10 = 40*25 color, 0x20 = 80*25 color, 0x30 = monochrome
          case 0x15:
            return (MemoryController.MemorySize / 1024) & 0xFF;
          case 0x16:
            return (MemoryController.MemorySize / 1024) >> 8;
          default:
            return data[index];
        }
      }

      return 0;
    }

    public void SetByte(int port, byte value)
    {
      if (port == 0x70)
      {
        index = (byte)(value & 0x7F);
        nmiDisabled = (value & 0x80) != 0;
        return;
      }
      if (port == 0x71)
      {
        var virtualTime = DateTime.Now + virtualTimeOffset;
        switch (index)
        {
          case 0x00:
            virtualTimeOffset += TimeSpan.FromSeconds(FromBcd(value) - virtualTime.Second);
            break;
          case 0x01:
            alarmSecond = FromBcd(value);
            break;
          case 0x02:
            virtualTimeOffset += TimeSpan.FromMinutes(FromBcd(value) - virtualTime.Minute);
            break;
          case 0x03:
            alarmMinute = FromBcd(value);
            break;
          case 0x04:
            virtualTimeOffset += TimeSpan.FromHours(FromBcd(value) - virtualTime.Hour);
            break;
          case 0x05:
            alarmHour = FromBcd(value);
            break;
          case 0x07:
            day = FromBcd(value);
            break;
          case 0x08:
            month = FromBcd(value);
            break;
          case 0x09:
            year = 1900 + FromBcd(value);
            break;
          case 0x0B:
            hours24 = (value & 0x02) != 0;
            isBcd = (value & 0x04) != 0;
            break;
          default:
            data[index] = value;
            break;
        }
      }
    }

    private byte ToBcd(int value)
    {
      return isBcd ? (byte)((value >> 4) * 10 + (value & 0xF)) : (byte)value;
    }

    private byte FromBcd(byte value)
    {
      return isBcd ? (byte)((value / 10) << 4 | (value % 10)) : value;
    }
  }
}
