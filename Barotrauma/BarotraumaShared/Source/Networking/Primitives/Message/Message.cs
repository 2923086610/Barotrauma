﻿using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Barotrauma.Networking
{
    public static class MsgConstants
    {
        public const int MTU = 1200;
        public const int CompressionThreshold = 1000;
    }

    /// <summary>
    /// Utility struct for writing Singles
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct SingleUIntUnion
    {
        /// <summary>
        /// Value as a 32 bit float
        /// </summary>
        [FieldOffset(0)]
        public float SingleValue;

        /// <summary>
        /// Value as an unsigned 32 bit integer
        /// </summary>
        [FieldOffset(0)]
        public uint UIntValue;
    }

    internal static class MsgWriter
    {
        internal static void Write(byte[] buf, ref int bitPos, bool val)
        {
#if DEBUG
            int resetPos = bitPos;
#endif

            int bytePos = bitPos / 8;
            int bitOffset = bitPos % 8;
            byte bitFlag = (byte)(1 << bitOffset);
            byte bitMask = (byte)((~bitFlag) & 0xff);
            buf[bytePos] &= bitMask;
            if (val) buf[bytePos] |= bitFlag;
            bitPos++;

#if DEBUG
            bool testVal = MsgReader.ReadBoolean(buf, ref resetPos);
            if (testVal != val || resetPos != bitPos)
            {
                DebugConsole.ThrowError("Boolean written incorrectly! " + testVal + ", " + val + "; " + resetPos + ", " + bitPos);
            }
#endif
        }

        internal static void WritePadBits(byte[] buf, ref int bitPos)
        {
            int bitOffset = bitPos % 8;
            bitPos += ((8 - bitOffset) % 8);
        }

        internal static void Write(byte[] buf, ref int bitPos, byte val)
        {
            NetBitWriter.WriteByte(val, 8, buf, bitPos);
            bitPos += 8;
        }

        internal static void Write(byte[] buf, ref int bitPos, UInt16 val)
        {
            NetBitWriter.WriteUInt16(val, 16, buf, bitPos);
            bitPos += 16;
        }

        internal static void Write(byte[] buf, ref int bitPos, Int16 val)
        {
            NetBitWriter.WriteUInt16((UInt16)val, 16, buf, bitPos);
            bitPos += 16;
        }

        internal static void Write(byte[] buf, ref int bitPos, UInt32 val)
        {
            NetBitWriter.WriteUInt32(val, 32, buf, bitPos);
            bitPos += 32;
        }

        internal static void Write(byte[] buf, ref int bitPos, Int32 val)
        {
            NetBitWriter.WriteUInt32((UInt32)val, 32, buf, bitPos);
            bitPos += 32;
        }

        internal static void Write(byte[] buf, ref int bitPos, UInt64 val)
        {
            NetBitWriter.WriteUInt64(val, 64, buf, bitPos);
            bitPos += 64;
        }

        internal static void Write(byte[] buf, ref int bitPos, Int64 val)
        {
            NetBitWriter.WriteUInt64((UInt64)val, 64, buf, bitPos);
            bitPos += 64;
        }

        internal static void Write(byte[] buf, ref int bitPos, Single val)
        {
            // Use union to avoid BitConverter.GetBytes() which allocates memory on the heap
            SingleUIntUnion su;
            su.UIntValue = 0; // must initialize every member of the union to avoid warning
            su.SingleValue = val;

            NetBitWriter.WriteUInt32(su.UIntValue, 32, buf, bitPos);
            bitPos += 32;
        }

        internal static void Write(byte[] buf, ref int bitPos, Double val)
        {
            byte[] bytes = BitConverter.GetBytes(val);
            WriteBytes(buf, ref bitPos, bytes, 0, bytes.Length);
            bitPos += 64;
        }
        internal static void Write(byte[] buf, ref int bitPos, string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                WriteVariableUInt32(buf, ref bitPos, (uint)0);
                return;
            }
            
            byte[] bytes = Encoding.UTF8.GetBytes(val);
            WriteVariableUInt32(buf, ref bitPos, (uint)bytes.Length);
            WriteBytes(buf, ref bitPos, bytes, 0, bytes.Length);
        }

        internal static int WriteVariableUInt32(byte[] buf, ref int bitPos, uint value)
        {
            int retval = 1;
            uint num1 = (uint)value;
            while (num1 >= 0x80)
            {
                Write(buf, ref bitPos, (byte)(num1 | 0x80));
                num1 = num1 >> 7;
                retval++;
            }
            Write(buf, ref bitPos, (byte)num1);
            return retval;
        }

        internal static void WriteRangedInteger(byte[] buf, ref int bitPos, int val, int min, int max)
        {
            uint range = (uint)(max - min);
            int numberOfBits = NetUtility.BitsToHoldUInt(range);

            uint rvalue = (uint)(val - min);
            NetBitWriter.WriteUInt32(rvalue, numberOfBits, buf, bitPos);
            bitPos += numberOfBits;
        }

        internal static void WriteRangedSingle(byte[] buf, ref int bitPos, Single val, Single min, Single max, int numberOfBits)
        {
            float range = max - min;
            float unit = ((val - min) / range);
            int maxVal = (1 << numberOfBits) - 1;
            
            NetBitWriter.WriteUInt32((UInt32)((float)maxVal * unit), numberOfBits, buf, bitPos);
            bitPos += numberOfBits;
        }

        internal static void WriteBytes(byte[] buf, ref int bitPos, byte[] val, int pos, int length)
        {
            NetBitWriter.WriteBytes(val, pos, length, buf, bitPos);
            bitPos += length * 8;
        }
    }

    internal static class MsgReader
    {
        internal static bool ReadBoolean(byte[] buf, ref int bitPos)
        {
            byte retval = NetBitWriter.ReadByte(buf, 1, bitPos);
            bitPos++;
            return (retval > 0 ? true : false);
        }

        internal static void ReadPadBits(byte[] buf, ref int bitPos)
        {
            int bitOffset = bitPos % 8;
            bitPos += (8 - bitOffset) % 8;
        }

        internal static byte ReadByte(byte[] buf, ref int bitPos)
        {
            byte retval = NetBitWriter.ReadByte(buf, 8, bitPos);
            bitPos += 8;
            return retval;
        }

        internal static UInt16 ReadUInt16(byte[] buf, ref int bitPos)
        {
            uint retval = NetBitWriter.ReadUInt16(buf, 16, bitPos);
            bitPos += 16;
            return (ushort)retval;
        }

        internal static Int16 ReadInt16(byte[] buf, ref int bitPos)
        {
            return (Int16)ReadUInt16(buf, ref bitPos);
        }

        internal static UInt32 ReadUInt32(byte[] buf, ref int bitPos)
        {
            uint retval = NetBitWriter.ReadUInt32(buf, 32, bitPos);
            bitPos += 32;
            return retval;
        }

        internal static Int32 ReadInt32(byte[] buf, ref int bitPos)
        {
            return (Int32)ReadUInt32(buf, ref bitPos);
        }

        internal static UInt64 ReadUInt64(byte[] buf, ref int bitPos)
        {
            ulong low = NetBitWriter.ReadUInt32(buf, 32, bitPos);
            bitPos += 32;
            ulong high = NetBitWriter.ReadUInt32(buf, 32, bitPos);
            ulong retval = low + (high << 32);
            bitPos += 32;
            return retval;
        }

        internal static Int64 ReadInt64(byte[] buf, ref int bitPos)
        {
            return (Int64)ReadUInt64(buf, ref bitPos);
        }

        internal static Single ReadSingle(byte[] buf, ref int bitPos)
        {
            if ((bitPos & 7) == 0) // read directly
            {
                float retval = BitConverter.ToSingle(buf, bitPos >> 3);
                bitPos += 32;
                return retval;
            }

            byte[] bytes = ReadBytes(buf, ref bitPos, 4);
            return BitConverter.ToSingle(bytes, 0);
        }

        internal static Double ReadDouble(byte[] buf, ref int bitPos)
        {
            if ((bitPos & 7) == 0) // read directly
            {
                // read directly
                double retval = BitConverter.ToDouble(buf, bitPos >> 3);
                bitPos += 64;
                return retval;
            }

            byte[] bytes = ReadBytes(buf, ref bitPos, 8);
            return BitConverter.ToDouble(bytes, 0);
        }

        internal static UInt32 ReadVariableUInt32(byte[] buf, ref int bitPos)
        {
            int bitLength = buf.Length * 8;

            int num1 = 0;
            int num2 = 0;
            while (bitLength - bitPos >= 8)
            {
                byte num3 = ReadByte(buf, ref bitPos);
                num1 |= (num3 & 0x7f) << num2;
                num2 += 7;
                if ((num3 & 0x80) == 0)
                    return (uint)num1;
            }

            // ouch; failed to find enough bytes; malformed variable length number?
            return (uint)num1;
        }

        internal static String ReadString(byte[] buf, ref int bitPos)
        {
            int bitLength = buf.Length * 8;
            int byteLen = (int)ReadVariableUInt32(buf, ref bitPos);

            if (byteLen <= 0) { return String.Empty; }

            if ((ulong)(bitLength - bitPos) < ((ulong)byteLen * 8))
            {
                // not enough data
				return null;
            }

            if ((bitPos & 7) == 0)
            {
                // read directly
                string retval = System.Text.Encoding.UTF8.GetString(buf, bitPos >> 3, byteLen);
                bitPos += (8 * byteLen);
                return retval;
            }

            byte[] bytes = ReadBytes(buf, ref bitPos, byteLen);
            return System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        internal static int ReadRangedInteger(byte[] buf, ref int bitPos, int min, int max)
        {
			uint range = (uint)(max - min);
			int numBits = NetUtility.BitsToHoldUInt(range);

			uint rvalue = NetBitWriter.ReadUInt32(buf, numBits, bitPos);
            bitPos += numBits;
            
            return (int)(min + rvalue);
        }

        internal static Single ReadRangedSingle(byte[] buf, ref int bitPos, Single min, Single max, int bitCount)
        {
            int maxInt = (1 << bitCount) - 1;
            int intVal = ReadRangedInteger(buf, ref bitPos, 0, maxInt);
            Single range = max - min;
            return min + (range * ((Single)intVal) / ((Single)maxInt));
        }

        internal static byte[] ReadBytes(byte[] buf, ref int bitPos, int numberOfBytes)
        {
            byte[] retval = new byte[numberOfBytes];
            NetBitWriter.ReadBytes(buf, numberOfBytes, bitPos, retval, 0);
            bitPos += (8 * numberOfBytes);
            return retval;
        }
    }

    public class WriteOnlyMessage : IWriteMessage
    {
        private byte[] buf = new byte[5000];
        private int seekPos = 0;
        private int lengthBits = 0;

        public int BitPosition
        {
            get
            {
                return seekPos;
            }
            set
            {
                seekPos = value;
            }
        }

        public int BytePosition
        {
            get
            {
                return seekPos / 8;
            }
        }

        public byte[] Buffer
        {
            get
            {
                return buf;
            }
        }

        public int LengthBits
        {
            get
            {
                lengthBits = seekPos > lengthBits ? seekPos : lengthBits;
                return lengthBits;
            }
            set
            {
                lengthBits = value;
                seekPos = seekPos > lengthBits ? lengthBits : seekPos;
            }
        }

        public int LengthBytes
        {
            get
            {
                return (LengthBits + ((8 - (LengthBits % 8)) % 8)) / 8;
            }
        }

        public void Write(bool val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void WritePadBits()
        {
            MsgWriter.WritePadBits(buf, ref seekPos);
        }

        public void Write(byte val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(UInt16 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Int16 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(UInt32 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Int32 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(UInt64 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Int64 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Single val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Double val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void WriteVariableUInt32(UInt32 val)
        {
            MsgWriter.WriteVariableUInt32(buf, ref seekPos, val);
        }

        public void Write(String val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void WriteRangedIntegerDeprecated(int min, int max, int val)
        {
            MsgWriter.WriteRangedInteger(buf, ref seekPos, val, min, max);
        }

        public void WriteRangedInteger(int val, int min, int max)
        {
            MsgWriter.WriteRangedInteger(buf, ref seekPos, val, min, max);
        }

        public void WriteRangedSingle(Single val, Single min, Single max, int bitCount)
        {
            MsgWriter.WriteRangedSingle(buf, ref seekPos, val, min, max, bitCount);
        }

        public void Write(byte[] val, int startPos, int length)
        {
            MsgWriter.WriteBytes(buf, ref seekPos, val, startPos, length);
        }

        public void PrepareForSending(byte[] outBuf, out bool isCompressed, out int length)
        {
            if (LengthBytes <= MsgConstants.CompressionThreshold)
            {
                isCompressed = false;
                Array.Copy(buf, outBuf, LengthBytes);
                length = LengthBytes;
            }
            else
            {
                isCompressed = true;
                using (MemoryStream output = new MemoryStream())
                {
                    using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Fastest))
                    {
                        dstream.Write(buf, 0, LengthBytes);
                    }
                    byte[] compressedBuf = output.ToArray();
                    Array.Copy(compressedBuf, outBuf, compressedBuf.Length);
                    length = compressedBuf.Length;
                }
                DebugConsole.NewMessage("Compressing message: " + LengthBytes + " to " + length);
            }
        }
    }

    public class ReadOnlyMessage : IReadMessage
    {
        private byte[] buf = new byte[5000];
        private int seekPos = 0;
        private int lengthBits = 0;

        public int BitPosition
        {
            get
            {
                return seekPos;
            }
            set
            {
                seekPos = value;
            }
        }

        public int BytePosition
        {
            get
            {
                return seekPos / 8;
            }
        }

        public byte[] Buffer
        {
            get
            {
                return buf;
            }
        }

        public int LengthBits
        {
            get
            {
                lengthBits = seekPos > lengthBits ? seekPos : lengthBits;
                return lengthBits;
            }
            set
            {
                lengthBits = value;
                seekPos = seekPos > lengthBits ? lengthBits : seekPos;
            }
        }

        public int LengthBytes
        {
            get
            {
                return lengthBits / 8;
            }
        }

        public NetworkConnection Sender { get; private set; }
        
        public ReadOnlyMessage(byte[] inBuf, bool isCompressed, int startPos, int inLength, NetworkConnection sender)
        {
            Sender = sender;
            if (isCompressed)
            {
                byte[] decompressedData;
                using (MemoryStream input = new MemoryStream(inBuf, startPos, inLength))
                {
                    using (MemoryStream output = new MemoryStream())
                    {
                        using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
                        {
                            dstream.CopyTo(output);
                        }
                        decompressedData = output.ToArray();
                    }
                }
                Array.Copy(decompressedData, 0, buf, 0, decompressedData.Length);
                lengthBits = decompressedData.Length * 8;
                DebugConsole.NewMessage("Decompressing message: " + inLength + " to " + LengthBytes);
            }
            else
            {
                Array.Copy(inBuf, startPos, buf, 0, inLength);
                lengthBits = inLength * 8;
            }
            seekPos = 0;
        }

        public bool ReadBoolean()
        {
            return MsgReader.ReadBoolean(buf, ref seekPos);
        }

        public void ReadPadBits()
        {
            MsgReader.ReadPadBits(buf, ref seekPos);
        }

        public byte ReadByte()
        {
            return MsgReader.ReadByte(buf, ref seekPos);
        }

        public UInt16 ReadUInt16()
        {
            return MsgReader.ReadUInt16(buf, ref seekPos);
        }

        public Int16 ReadInt16()
        {
            return MsgReader.ReadInt16(buf, ref seekPos);
        }

        public UInt32 ReadUInt32()
        {
            return MsgReader.ReadUInt32(buf, ref seekPos);
        }

        public Int32 ReadInt32()
        {
            return MsgReader.ReadInt32(buf, ref seekPos);
        }

        public UInt64 ReadUInt64()
        {
            return MsgReader.ReadUInt64(buf, ref seekPos);
        }

        public Int64 ReadInt64()
        {
            return MsgReader.ReadInt64(buf, ref seekPos);
        }

        public Single ReadSingle()
        {
            return MsgReader.ReadSingle(buf, ref seekPos);
        }

        public Double ReadDouble()
        {
            return MsgReader.ReadDouble(buf, ref seekPos);
        }

        public UInt32 ReadVariableUInt32()
        {
            return MsgReader.ReadVariableUInt32(buf, ref seekPos);
        }

        public String ReadString()
        {
            return MsgReader.ReadString(buf, ref seekPos);
        }

        public int ReadRangedInteger(int min, int max)
        {
            return MsgReader.ReadRangedInteger(buf, ref seekPos, min, max);
        }

        public Single ReadRangedSingle(Single min, Single max, int bitCount)
        {
            return MsgReader.ReadRangedSingle(buf, ref seekPos, min, max, bitCount);
        }

        public byte[] ReadBytes(int numberOfBytes)
        {
            return MsgReader.ReadBytes(buf, ref seekPos, numberOfBytes);
        }
    }

    public class ReadWriteMessage : IWriteMessage, IReadMessage
    {
        private byte[] buf = new byte[5000];
        private int seekPos = 0;
        private int lengthBits = 0;

        public int BitPosition
        {
            get
            {
                return seekPos;
            }
            set
            {
                seekPos = value;
            }
        }

        public int BytePosition
        {
            get
            {
                return seekPos / 8;
            }
        }

        public byte[] Buffer
        {
            get
            {
                return buf;
            }
        }

        public int LengthBits
        {
            get
            {
                lengthBits = seekPos > lengthBits ? seekPos : lengthBits;
                return lengthBits;
            }
            set
            {
                lengthBits = value;
                seekPos = seekPos > lengthBits ? lengthBits : seekPos;
            }
        }

        public int LengthBytes
        {
            get
            {
                return (LengthBits + ((8 - (LengthBits % 8)) % 8)) / 8;
            }
        }

        public NetworkConnection Sender { get { return null; } }

        public void Write(bool val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void WritePadBits()
        {
            MsgWriter.WritePadBits(buf, ref seekPos);
        }

        public void Write(byte val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(UInt16 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Int16 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(UInt32 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Int32 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(UInt64 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Int64 val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Single val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void Write(Double val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void WriteVariableUInt32(UInt32 val)
        {
            MsgWriter.WriteVariableUInt32(buf, ref seekPos, val);
        }

        public void Write(String val)
        {
            MsgWriter.Write(buf, ref seekPos, val);
        }

        public void WriteRangedIntegerDeprecated(int min, int max, int val)
        {
            MsgWriter.WriteRangedInteger(buf, ref seekPos, val, min, max);
        }

        public void WriteRangedInteger(int val, int min, int max)
        {
            MsgWriter.WriteRangedInteger(buf, ref seekPos, val, min, max);
        }

        public void WriteRangedSingle(Single val, Single min, Single max, int bitCount)
        {
            MsgWriter.WriteRangedSingle(buf, ref seekPos, val, min, max, bitCount);
        }

        public void Write(byte[] val, int startPos, int length)
        {
            MsgWriter.WriteBytes(buf, ref seekPos, val, startPos, length);
        }

        public bool ReadBoolean()
        {
            return MsgReader.ReadBoolean(buf, ref seekPos);
        }

        public void ReadPadBits()
        {
            MsgReader.ReadPadBits(buf, ref seekPos);
        }

        public byte ReadByte()
        {
            return MsgReader.ReadByte(buf, ref seekPos);
        }

        public UInt16 ReadUInt16()
        {
            return MsgReader.ReadUInt16(buf, ref seekPos);
        }

        public Int16 ReadInt16()
        {
            return MsgReader.ReadInt16(buf, ref seekPos);
        }

        public UInt32 ReadUInt32()
        {
            return MsgReader.ReadUInt32(buf, ref seekPos);
        }

        public Int32 ReadInt32()
        {
            return MsgReader.ReadInt32(buf, ref seekPos);
        }

        public UInt64 ReadUInt64()
        {
            return MsgReader.ReadUInt64(buf, ref seekPos);
        }

        public Int64 ReadInt64()
        {
            return MsgReader.ReadInt64(buf, ref seekPos);
        }

        public Single ReadSingle()
        {
            return MsgReader.ReadSingle(buf, ref seekPos);
        }

        public Double ReadDouble()
        {
            return MsgReader.ReadDouble(buf, ref seekPos);
        }

        public UInt32 ReadVariableUInt32()
        {
            return MsgReader.ReadVariableUInt32(buf, ref seekPos);
        }

        public String ReadString()
        {
            return MsgReader.ReadString(buf, ref seekPos);
        }

        public int ReadRangedInteger(int min, int max)
        {
            return MsgReader.ReadRangedInteger(buf, ref seekPos, min, max);
        }

        public Single ReadRangedSingle(Single min, Single max, int bitCount)
        {
            return MsgReader.ReadRangedSingle(buf, ref seekPos, min, max, bitCount);
        }

        public byte[] ReadBytes(int numberOfBytes)
        {
            return MsgReader.ReadBytes(buf, ref seekPos, numberOfBytes);
        }

        public void PrepareForSending(byte[] outBuf, out bool isCompressed, out int outLength)
        {
            throw new InvalidOperationException("ReadWriteMessages are not to be sent");
        }
    }
}
