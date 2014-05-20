using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoMap.Helpers
{
  public class BigEndianReader
  {
    public BigEndianReader(BinaryReader baseReader)
    {
      mBaseReader = baseReader;
    }

    public short ReadInt16()
    {
      return BitConverter.ToInt16(ReadBigEndianBytes(2), 0);
    }

    public ushort ReadUInt16()
    {
      return BitConverter.ToUInt16(ReadBigEndianBytes(2), 0);
    }

    public uint ReadUInt32()
    {
      return BitConverter.ToUInt32(ReadBigEndianBytes(4), 0);
    }
    public int ReadInt32()
    {
      return BitConverter.ToInt32(ReadBigEndianBytes(4), 0);
    }

    public float ReadFloat()
    {
      return BitConverter.ToSingle(ReadBigEndianBytes(4), 0);
    }

    public string ReadChars(int count)
    {
      return new String(mBaseReader.ReadChars(count));
    }

    public void Skip(int count)
    {
      mBaseReader.BaseStream.Seek(count, SeekOrigin.Current);
      //mBaseReader.ReadBytes(count);
    }

    public byte[] ReadBigEndianBytes(int count)
    {
      byte[] bytes = new byte[count];
      for (int i = count - 1; i >= 0; i--)
        bytes[i] = mBaseReader.ReadByte();

      return bytes;
    }

    public byte[] ReadBytes(int count)
    {
      return mBaseReader.ReadBytes(count);
    }

    public void Close()
    {
      mBaseReader.Close();
    }

    public Stream BaseStream
    {
      get { return mBaseReader.BaseStream; }
    }

    private BinaryReader mBaseReader;
  }
}
