using System;
using System.Configuration;

namespace Pixockets.Extensions
{
    public static class BitWriter
    {
        public static int Zero(this byte[] buffer, int length, int pos)
        {
            Array.Clear(buffer, pos, length);
            return pos + length;
        }

        public static int WriteUInt16(this byte[] buffer, ushort value, int pos)
        {
            buffer[pos++] = (byte)(value);
            buffer[pos++] = (byte)(value >> 8);

            return pos;
        }

        public static int WriteUInt32(this byte[] buffer, uint value, int pos)
        {
            buffer[pos++] = (byte)(value);
            buffer[pos++] = (byte)(value >> 8);
            buffer[pos++] = (byte)(value >> 16);
            buffer[pos++] = (byte)(value >> 24);

            return pos;
        }

        public static int WriteUInt64(this byte[] buffer, ulong value, int pos)
        {
            buffer[pos++] = (byte)(value);
            buffer[pos++] = (byte)(value >> 8);
            buffer[pos++] = (byte)(value >> 16);
            buffer[pos++] = (byte)(value >> 24);
            buffer[pos++] = (byte)(value >> 32);
            buffer[pos++] = (byte)(value >> 40);
            buffer[pos++] = (byte)(value >> 48);
            buffer[pos++] = (byte)(value >> 56);
            return pos;
        }

        public static int WriteString8(this byte[] buffer, string value, int pos)
        {
            var len = System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, pos + 1);
            if (len >= 255)
                throw new ArgumentOutOfRangeException(nameof(value), "Can't encode line with more than 254 bytes");

            buffer[pos++] = (byte)len;

            pos += len;
            return pos;
        }

        public static int WriteString16(this byte[] buffer, string value, int pos)
        {
            var len = System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, pos + 2);
            if (len >= 65535)
                throw new ArgumentOutOfRangeException(nameof(value), "Can't encode line with more than 65534 bytes");

            buffer[pos++] = (byte)len;
            buffer[pos++] = (byte)(len >> 8);

            pos += len;
            return pos;
        }

        public static int ReadString8(this byte[] buffer, out string value, int pos)
        {
            if (pos >= buffer.Length)
            {
                value = string.Empty;
                return pos;
            }
            int len = buffer[pos++];
            if (pos + len > buffer.Length)
            {
                value = string.Empty;
                return pos;
            }
            value = System.Text.Encoding.UTF8.GetString(buffer, pos, len);
            pos += len;
            return pos;
        }
        public static int ReadString16(this byte[] buffer, out string value, int pos)
        {
            if (pos + 1 >= buffer.Length)
            {
                value = string.Empty;
                return pos;
            }
            int len = buffer[pos++];
            len += (buffer[pos++] << 8);
            if (pos + len > buffer.Length)
            {
                value = string.Empty;
                return pos;
            }
            value = System.Text.Encoding.UTF8.GetString(buffer, pos, len);
            pos += len;
            return pos;
        }
    }
}
