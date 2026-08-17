using System;
using System.IO;
using System.Text;

namespace MapleLib.PacketLib
{
    public class PacketReader : IDisposable
    {
        protected MemoryStream _buffer;
        private readonly BinaryReader _binReader;

        public PacketReader(byte[] arrayOfBytes)
        {
            _buffer = new MemoryStream(arrayOfBytes, false);
            _binReader = new BinaryReader(_buffer, Encoding.Default);
        }

        bool disposed = false;

        public void Dispose()
        {
            if (disposed)
                return;
            _binReader.Close();
            _buffer.Dispose();
            disposed = true;
        }

        public void Skip(int length)
        {
            _buffer.Position += length;
        }

        public byte ReadByte()
        {
            return _binReader.ReadByte();
        }

        public byte[] ReadBytes(int count)
        {
            return _binReader.ReadBytes(count);
        }

        public short ReadShort()
        {
            return _binReader.ReadInt16();
        }

        public string ReadMapleString()
        {
            return Encoding.Default.GetString(ReadBytes(ReadShort()));
        }
    }
}
