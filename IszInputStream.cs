using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
<<<<<<< HEAD
using Misho.IO;
=======
>>>>>>> fcd3a3158fe04c7ede14db88a87ab1c713fd14b7

namespace DiskImage
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct IszHeader
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string Signature;
        public byte HeaderSize;
        public byte VersionNumber;
        public UInt32 VolumeSerialNumber;
        public UInt16 SectorSize;
        public UInt32 TotalSectors;
        public IszPasswordType EncryptionType;
        public Int64 SegmentSize;
        public UInt32 NBlocks;
        public UInt32 BlockSize;
        public byte PointerLength;
        public sbyte FileSegmentNumber;
        public UInt32 ChunkPointersOffset;
        public UInt32 SegmentPointersOffset;
        public UInt32 DataOffset;
        public byte Reserved;
        public UInt32 Checksum1;
        public UInt32 Size1;
        public UInt32 Unknown2;
        public UInt32 Checksum2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IszSegment
    {
        public Int64 Size;
        public UInt32 NumberOfChunks;
        public UInt32 FirstChunckNumber;
        public UInt32 ChunkOffset;
        public UInt32 LeftSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IszChunkPointer
    {
        public uint DataType;
        public uint DataSize;
    }

    internal enum IszPasswordType : byte
    {
        NoPassword = 0,
        PasswordProtected = 1, // not used
        AES128 = 2,
        AES192 = 3,
        AES256 = 4
    }

    internal enum IszCompression
    {
        Uncompressed = 1,
        Zip = 2,
        BZip2 = 3
    }

    internal class IszBinaryReader : BinaryReader
    {
        public IszBinaryReader(Stream stream)
            : base(stream)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(false);
        }
    }

    internal class IszNotSupportedException : NotSupportedException
    {
        public IszNotSupportedException(string message)
            : base(string.Format("method {0}.{1} not supported.", typeof(IszInputStream).Name, message))
        {
        }
    }

    public class IszInputStream : Stream
    {
        private const string ISZMagic = "IsZ!";

        internal IszHeader header;
        internal IszChunkPointer[] chunkPointers;
        internal IszSegment[] segments;


        private Stream baseStream;
        private Crc32 uncompressedCrc32;
        private Crc32 compressedCrc32;
        private int currentBlockID;
        private byte[] dataBuffer;
        private int dataBufferPos;

        public IszInputStream(Stream stream)
        {
            baseStream = (stream != null && stream.CanRead && stream.CanSeek) ? stream : null;

            ReadHeader();
            segments = ReadSegments();
            chunkPointers = ReadChunkPointers();
            currentBlockID = 0;
            dataBuffer = null;
            dataBufferPos = 0;

            uncompressedCrc32 = new Crc32();
            uncompressedCrc32.Value = 0;

            compressedCrc32 = new Crc32();
            compressedCrc32.Value = 0;
        }

        public long UncompressedLength
        {
            get
            {
                return header.SectorSize * header.TotalSectors;
            }
        }

        private byte[] XorObfuscate(byte[] data)
        {
            byte[] code = new byte[] { 0xb6, 0x8c, 0xa5, 0xde };

            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= code[i % 4];
            }

            return data;
        }

        internal bool HeaderValidate(IszHeader header, bool suppressException = true)
        {
            if (string.Compare(header.Signature, ISZMagic) != 0)
            {
                if (!suppressException) throw new IOException("Invalid ISZ file");
                return false;
            }

            if (header.HeaderSize != Marshal.SizeOf(typeof(IszHeader)))
            {
                if (!suppressException) throw new IOException("Invalid ISZ header size");
                return false;
            }

            if (header.VersionNumber != 1)
            {
                if (!suppressException) throw new IOException("Unsupported ISZ file version");
                return false;
            }
            return true;
        }

        private void ReadHeader()
        {
            if (baseStream == null)
            {
                throw new IOException("Cannot read ISZ file.");
            }

            using (IszBinaryReader reader = new IszBinaryReader(baseStream))
            {
                header = new IszHeader();
                header.Signature = Encoding.Default.GetString(reader.ReadBytes(4));
                header.HeaderSize = reader.ReadByte();
                header.VersionNumber = reader.ReadByte();
                header.VolumeSerialNumber = reader.ReadUInt32();
                header.SectorSize = reader.ReadUInt16();
                header.TotalSectors = reader.ReadUInt32();
                header.EncryptionType = (IszPasswordType)reader.ReadByte();
                header.SegmentSize = reader.ReadInt64();
                header.NBlocks = reader.ReadUInt32();
                header.BlockSize = reader.ReadUInt32();
                header.PointerLength = reader.ReadByte();
                header.FileSegmentNumber = reader.ReadSByte();
                header.ChunkPointersOffset = reader.ReadUInt32();
                header.SegmentPointersOffset = reader.ReadUInt32();
                header.DataOffset = reader.ReadUInt32();
                header.Reserved = reader.ReadByte();
                header.Checksum1 = reader.ReadUInt32();
                header.Size1 = reader.ReadUInt32();
                header.Unknown2 = reader.ReadUInt32();
                header.Checksum2 = reader.ReadUInt32();

                HeaderValidate(header, false);
            }
        }

        internal IszSegment ReadSegment()
        {
            IszSegment sdt = new IszSegment();

            using (IszBinaryReader reader = new IszBinaryReader(baseStream))
            {
                sdt.Size = reader.ReadInt64();
                sdt.NumberOfChunks = reader.ReadUInt32();
                sdt.FirstChunckNumber = reader.ReadUInt32();
                sdt.ChunkOffset = reader.ReadUInt32();
                sdt.LeftSize = reader.ReadUInt32();
            }
            //int bufferSize = Marshal.SizeOf(typeof(IszSegment));
            //byte[] readBuffer = new byte[bufferSize];
            //stream.Read(readBuffer, 0, bufferSize);
            //GCHandle handle = GCHandle.Alloc(readBuffer, GCHandleType.Pinned);
            //sdt = (IszSegment)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(IszSegment));
            //handle.Free();

            return sdt;
        }

        internal IszSegment[] ReadSegments()
        {
            List<IszSegment> segmentsList = new List<IszSegment>();
            IszSegment segment;

            if (header.SegmentPointersOffset == 0)
            {
                segment = new IszSegment();
                segment.Size = 0;
                segment.NumberOfChunks = header.NBlocks;
                segment.FirstChunckNumber = 0;
                segment.ChunkOffset = header.DataOffset;
                segment.LeftSize = 0;
                segmentsList.Add(segment);
            }
            else
            {
                if (baseStream.Seek(header.SegmentPointersOffset, SeekOrigin.Begin) != header.SegmentPointersOffset)
                {
                    throw new IOException("Cannot seek in ISZ file.");
                }

                segment = ReadSegment();
                while (segment.Size != 0)
                {
                    segmentsList.Add(segment);
                    segment = ReadSegment();
                }
            }

            return segmentsList.ToArray();
        }

        internal IszChunkPointer[] ReadChunkPointers()
        {
            List<IszChunkPointer> chunksList = new List<IszChunkPointer>();

            if (header.ChunkPointersOffset == 0)
            {
                chunksList.Add(new IszChunkPointer() { DataType = 1, DataSize = header.Size1 });
                return chunksList.ToArray();
            }

            if (header.PointerLength != 3)
            {
                throw new Exception("Only pointer sizes of 3 implemented");
            }

            uint TableSize = header.PointerLength * header.NBlocks;

            if (baseStream.Seek(header.ChunkPointersOffset, SeekOrigin.Begin) != header.ChunkPointersOffset)
            {
                throw new IOException("Cannot seek in ISZ file.");
            }

            byte[] data = new byte[TableSize];

            if (baseStream.Read(data, 0, (int)TableSize) != data.Length)
            {
                throw new IOException(string.Format("Cannot read {0} bytes from ISZ file.", data.Length));
            }

            byte[] data2 = XorObfuscate(data);
            Array.Resize(ref data, 3);

            for (uint i = 0; i < header.NBlocks; i++)
            {
                Array.Copy(data2, i * 3, data, 0, 3);
                uint val = (uint)(data[2] * 256 * 256) + (uint)(data[1] * 256) + (uint)data[0];
                uint dataType = val >> 22;
                uint dataSize = val & 0x3fffff;
                chunksList.Add(new IszChunkPointer() { DataSize = dataSize, DataType = dataType });
            }

            return chunksList.ToArray();
        }

        private byte[] ReadData(uint offset, int size)
        {
            if (baseStream.Seek(offset, SeekOrigin.Begin) != offset)
            {
                throw new IOException("Cannot seek in ISZ file.");
            }

            byte[] buffer = new byte[size];

            if (baseStream.Read(buffer, 0, size) != size)
            {
                throw new IOException(string.Format("Cannot read {0} bytes from ISZ file.", size));
            }
            else
            {
                return buffer;
            }
        }

        private byte[] ReadBlock(int blockId)
        {
            IszSegment segment;
            uint firstBlockId, lastBlockId;
            uint currOffset = 0;

            if (segments.Length == 0)
                throw new Exception(string.Format("Unable to find the segment of block {0}", blockId));

            byte[] data = null, data2 = null;

            for (int segId = 0; segId < segments.Length; segId++)
            {
                segment = segments[segId];
                firstBlockId = segment.FirstChunckNumber;
                lastBlockId = segment.FirstChunckNumber + (segment.NumberOfChunks - 1);

                if (blockId >= firstBlockId && blockId <= lastBlockId)
                {
                    currOffset = segment.ChunkOffset;
                }

                for (int i = (int)segment.FirstChunckNumber; i < blockId; i++)
                {
                    if (chunkPointers[i].DataType != 0)
                        currOffset += chunkPointers[i].DataSize;
                }

                uint SizeToRead = chunkPointers[blockId].DataSize;

                if (blockId == lastBlockId)
                {
                    SizeToRead -= segment.LeftSize;
                }

                data = ReadData(currOffset, (int)SizeToRead);

                if (blockId == lastBlockId && segment.LeftSize != 0)
                {
                    data2 = ReadData(64, (int)segment.LeftSize);
                    Array.Resize(ref data, data.Length + data2.Length);
                    Array.Copy(data2, 0, data, data.Length - data2.Length, data2.Length);
                }
            }

            return data;
        }

        private byte[] ZipDecompressBlock(byte[] data)
        {
            byte[] buffer = new byte[data.Length];
            byte[] outBuffer = null;

<<<<<<< HEAD
            using (ByteArrayStream memStream = new ByteArrayStream(data))
=======
            using (MemoryStream memStream = new MemoryStream(data))
>>>>>>> fcd3a3158fe04c7ede14db88a87ab1c713fd14b7
            using (InflaterInputStream zipInputStream = new InflaterInputStream(memStream))
            {
                int readed, i = 0;

                while ((readed = zipInputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (readed > 0)
                    {
                        if (outBuffer == null)
                            outBuffer = new byte[readed];
                        else
                            Array.Resize(ref outBuffer, outBuffer.Length + readed);
                        Array.Copy(buffer, 0, outBuffer, i, readed);
                        i += readed;
                    }
                }
            }

            return outBuffer;
        }

        private byte[] BZip2DecompressBlock(byte[] data)
        {
            byte[] buffer = new byte[data.Length];
            byte[] outBuffer = null;

            data[0] = (byte)'B';
            data[1] = (byte)'Z';
            data[2] = (byte)'h';

<<<<<<< HEAD
            using (ByteArrayStream memStream = new ByteArrayStream(data))
=======
            //byte[] bz2MagicPatch = Encoding.ASCII.GetBytes("BZh");
            //Array.Copy(bz2MagicPatch, data, bz2MagicPatch.Length);

            using (MemoryStream memStream = new MemoryStream(data))
>>>>>>> fcd3a3158fe04c7ede14db88a87ab1c713fd14b7
            using (BZip2InputStream zipInputStream = new BZip2InputStream(memStream))
            {
                int readed, i = 0;

                while ((readed = zipInputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (readed > 0)
                    {
                        if (outBuffer == null)
                            outBuffer = new byte[readed];
                        else
                            Array.Resize(ref outBuffer, outBuffer.Length + readed);
                        Array.Copy(buffer, 0, outBuffer, i, readed);
                        i += readed;
                    }
                }
            }

            return outBuffer;
        }

        private byte[] DecompressBlock(int blockId)
        {
            if (chunkPointers[blockId].DataType == 0)
            {
                return BitConverter.GetBytes(chunkPointers[blockId].DataSize);
            }
            else
            {
                byte[] data = ReadBlock(blockId);

                if (data != null)
                {
                    compressedCrc32.Update(data);
                }

                switch ((IszCompression)chunkPointers[blockId].DataType)
                {
                    case IszCompression.Uncompressed:
                        break;
                    case IszCompression.Zip:
                        data = ZipDecompressBlock(data);
                        break;
                    case IszCompression.BZip2:
                        data = BZip2DecompressBlock(data);
                        break;
                    default:
                        data = null;
                        break;
                }


                if (data != null)
                {
                    uncompressedCrc32.Update(data);
                }

                return data;
            }
        }

        public override bool CanRead
        {
            get
            {
                return baseStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void Flush()
        {
            throw new IszNotSupportedException(MethodBase.GetCurrentMethod().Name);
        }

        public override long Length
        {
            get { return baseStream.Length; }
        }

        public override long Position
        {
            get
            {
                return baseStream.Position;
            }
            set
            {
                throw new IszNotSupportedException(MethodBase.GetCurrentMethod().Name);
            }
        }

        private bool FillBuffer()
        {
            if (currentBlockID < chunkPointers.Length)
            {
                dataBuffer = DecompressBlock(currentBlockID);
                dataBufferPos = 0;

                currentBlockID++;
                return dataBuffer != null && dataBufferPos < dataBuffer.Length;
            }
            else
                return false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (dataBuffer == null)
            {
                if (!FillBuffer()) return 0;
            }

            if (offset + count > buffer.Length)
            {
                count = buffer.Length - offset;
            }

            int outBufferPos = 0, size;

            while (count > 0)
            {
                if (dataBufferPos >= dataBuffer.Length && !FillBuffer())
                {
                    long crc = compressedCrc32.Value;

                    crc = ~crc & 0xffffffff;

                    if (crc != header.Checksum2)
                    {
                        throw new Exception("Input data is corrupted");
                    }

                    crc = uncompressedCrc32.Value;

                    crc = ~crc & 0xffffffff;

                    if (crc != header.Checksum1)
                    {
                            throw new Exception("CRC error during extraction data");
                    }

                    break;
                }

                size = Math.Min(count, dataBuffer.Length - dataBufferPos);
                Array.Copy(dataBuffer, dataBufferPos, buffer, offset + outBufferPos, size);

                count -= size;
                dataBufferPos += size;
                outBufferPos += size;
            }

            return outBufferPos;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new IszNotSupportedException(MethodBase.GetCurrentMethod().Name);
        }

        public override void SetLength(long value)
        {
            throw new IszNotSupportedException(MethodBase.GetCurrentMethod().Name);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new IszNotSupportedException(MethodBase.GetCurrentMethod().Name);
        }
    }
}
