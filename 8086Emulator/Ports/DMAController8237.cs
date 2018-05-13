using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Masch._8086Emulator.Ports
{
  // ReSharper disable once InconsistentNaming
  [SuppressMessage("ReSharper", "UnusedMember.Local")]
  public class DMAController8237 : IInternalDevice
  {
    private const int ChannelCount = 4;

    private readonly Channel[] channels;
    //private CommandRegister commandRegister;
    //private byte requestRegister;
    private byte statusRegister;

    public DMAController8237()
    {
      channels = new Channel[ChannelCount];
      for (var i = 0; i < ChannelCount; i++) { channels[i] = new Channel(); }
    }

    public IEnumerable<int> PortNumbers => Enumerable.Range(0x00, 16) /*.Concat(Enumerable.Range(0x80, 16))*/;

    public byte GetByte(int port)
    {
      byte result = 0;

      switch (port & 0x7F)
      {
        case 0x00: // channel 0 address	byte
        case 0x02: // channel 1 address	byte
        case 0x04: // channel 2 address	byte
        case 0x06: // channel 3 address	byte
        {
          var channel = channels[port >> 1];
          if (channel.Flipflop)
          {
            result = (byte)(channel.Address >> 8);
          }
          else
          {
            result = (byte)channel.Address;
          }
          channel.Flipflop = !channel.Flipflop;
          break;
        }
        case 0x01: // channel 0 word count byte
        case 0x03: // channel 1 word count byte
        case 0x05: // channel 2 word count byte
        case 0x07: // channel 3 word count byte
        {
          var channel = channels[port >> 1];
          if (channel.Flipflop)
          {
            result = (byte)(channel.Count >> 8);
          }
          else
          {
            result = (byte)channel.Count;
          }
          channel.Flipflop = !channel.Flipflop;
          break;
        }
        case 0x08: // channel 0-3 status register
          result = statusRegister;
          statusRegister &= 0xF0;
          break;
        case 0x0A: // channel 0-3 mask register
          for (int i = 0; i < ChannelCount; i++)
          {
            if (channels[i].Masked) { result |= (byte)(1 << i); }
          }
          break;
      }

      return result;
    }

    public void SetByte(int port, byte value)
    {
      switch (port & 0x7F)
      {
        case 0x00: // channel 0 address	byte
        case 0x02: // channel 1 address	byte
        case 0x04: // channel 2 address	byte
        case 0x06: // channel 3 address	byte
        {
          var channel = channels[port >> 1];
          if (channel.Flipflop)
          {
            channel.Address = (ushort)((value << 8) | (channel.Address & 0x00FF));
          }
          else
          {
            channel.Address = (ushort)((channel.Address & 0xFF00) | value);
          }
          channel.Flipflop = !channel.Flipflop;
          break;
        }
        case 0x01: // channel 0 word count byte
        case 0x03: // channel 1 word count byte
        case 0x05: // channel 2 word count byte
        case 0x07: // channel 3 word count byte
        {
          var channel = channels[port >> 1];
          if (channel.Flipflop)
          {
            channel.Count = (ushort)((value << 8) | (channel.Count & 0x00FF));
          }
          else
          {
            channel.Count = (ushort)((channel.Count & 0xFF00) | value);
          }
          channel.Flipflop = !channel.Flipflop;
          break;
        }
        case 0x08: // channel 0-3 command register
          //commandRegister = (CommandRegister)value;
          break;
        case 0x09: // write request register
          //requestRegister = value;
          break;
        case 0x0A: // channel 0-3 mask register
          channels[value & 0b11].Masked = (value & 0b100) != 0;
          break;
        case 0x0B: // channel 0-3 mode register
        {
          var channel = channels[value & 0b11];
          channel.Operation = (Operation)((value >> 1) & 0b11);
          channel.AutoInit = (value & 0x10) != 0;
          channel.AddressDecrement = (value & 0x20) != 0;
          channel.Mode = (Mode)(value >> 6);
          break;
        }
        case 0x0C: // clear byte pointer flip-flop
          Array.ForEach(channels, channel => channel.Flipflop = false);
          break;
        case 0x0D: // master clear
          Array.ForEach(channels, channel =>
          {
            channel.AddressDecrement = false;
            channel.AutoInit = false;
            channel.Masked = true;
            channel.Mode = default(Mode);
            channel.Operation = default(Operation);
          });
          statusRegister = 0;
          //commandRegister = CommandRegister.None;
          //requestRegister = 0;
          break;
        case 0x0E: // clear mask register
          Array.ForEach(channels, channel => channel.Masked = false);
          break;
        case 0x0F: // write mask register
          for (var i = 0; i < ChannelCount; i++)
          {
            channels[i].Masked = (value & (1 << i)) != 0;
          }
          break;
      }
    }

    private class Channel
    {
      public ushort Address;
      public bool AddressDecrement;
      public bool AutoInit;
      public ushort Count;
      public bool Flipflop;
      public bool Masked;
      public Mode Mode;
      public Operation Operation;
    }

    private enum Operation : byte
    {
      Verify,
      Write,
      Read
    }

    private enum Mode : byte
    {
      Demand,
      Single,
      Block,
      Cascade
    }

    [Flags]
    private enum CommandRegister : byte
    {
      None = 0,
      EnableController = 1 << 2,
      CompressedTiming = 1 << 3,
      RotatingPriority = 1 << 4,
      ExtendedWriteSelection = 1 << 5,
      DreqSenseActiveHigh = 1 << 6,
      DackSenseActiveHigh = 1 << 7
    }
  }
}