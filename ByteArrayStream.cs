using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Misho.IO
{
    public class ByteArrayStream : Stream
    {
        public const int DEFAULT_SIZE = 1024;

        /** Current position in the buffer where data will be read or written. */
        private int position;
        /** The buffer to be used for reading/writing. */
        private byte[] buffer;
        /** The used size of the current buffer. */
        private int bufferSize;
        /** The number of bytes by which the buffer size will increase as needed. */
        private int sizeIncrement;

        public override bool CanRead 
        {
            get { return (buffer != null && buffer.Length > 0); }
        }

        public override bool CanSeek 
        {
            get { return (buffer != null && buffer.Length > 0); }
        }

        public override bool CanWrite
        {
            get { return (buffer != null && buffer.Length > 0); }
        }

        public override long Position 
        {
            get { return position; }
            set 
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override long Length 
        {
            get { return buffer.Length; }
        }

        public ByteArrayStream()
            : this(new byte[DEFAULT_SIZE], 0)
        {
        }

        public ByteArrayStream(byte[] buf)
            : this(buf, buf.Length)
        {
        }

        public ByteArrayStream(byte[] buf, int size)
            : this(buf, size, DEFAULT_SIZE)
        {
        }

        public ByteArrayStream(byte[] buf, int size, int increment)
        {
            this.buffer = buf;
            this.position = 0;
            this.bufferSize = size;
            this.sizeIncrement = increment;
        }

        private void EnsureWrite(int length)
        {
            int newPos = position + length;
            int diff = newPos - buffer.Length;
            int inc = sizeIncrement > diff ? sizeIncrement : diff;

            if (newPos > buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length + inc);
            }

            if (newPos > bufferSize)
            {
                bufferSize = newPos;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Available()
        {
            return buffer.Length - position;
        }

        public override void Close()
        {
            base.Close();
        }

        public override int ReadByte()
        {
            return (int)buffer[position++];
        }

        public byte Read()
        {
            return (byte)ReadByte();
        }

        public int Read(byte[] buffer)
        {
            return Read(buffer, 0, buffer.Length);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = Available();

            if (count < bytesRead)
            {
                bytesRead = count;
            }

            Array.Copy(this.buffer, this.position, buffer, offset, bytesRead);
            this.position += bytesRead;

            return bytesRead;
        }

        public override void WriteByte(byte value)
        {
            EnsureWrite(1);
            buffer[position++] = value;
        }

        public void Write(byte value)
        {
            WriteByte(value);
        }

        public void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWrite(count);

            Array.Copy(buffer, offset, this.buffer, this.position, count);
            position += count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            int pos = 0;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    pos = (int)offset;
                    break;
                case SeekOrigin.Current:
                    pos = this.position + (int)offset;
                    break;
                case SeekOrigin.End:
                    pos = (buffer.Length-1) + (int)offset;
                    break;
            }

            if (pos >= 0 && pos < buffer.Length)
            {
                this.position = pos;
            }
            else
            {
                throw new IOException("Cannot seek outside the buffer");
            }

            return this.position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            if (value != buffer.Length)
            {
                Array.Resize(ref buffer, (int)value);

                if (position >= buffer.Length)
                {
                    position = buffer.Length - 1;
                }
            }
        }
    }
}
