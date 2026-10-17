using System;
using System.Text;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Escritura binaria compacta (little-endian) sobre un búfer reutilizable. Los mensajes de red
    /// se serializan aquí y la sesión copia los bytes al transporte; así el protocolo se puede
    /// probar sin red.
    /// </summary>
    public sealed class NetWriter
    {
        byte[] buffer;
        int length;

        public NetWriter(int capacity = 256)
        {
            buffer = new byte[Mathf.Max(16, capacity)];
        }

        public int Length => length;
        public byte[] Buffer => buffer;

        public void Clear() => length = 0;

        public ArraySegment<byte> ToSegment() => new ArraySegment<byte>(buffer, 0, length);

        public byte[] ToArray()
        {
            var copy = new byte[length];
            Array.Copy(buffer, copy, length);
            return copy;
        }

        void Ensure(int extra)
        {
            if (length + extra <= buffer.Length) return;
            int size = buffer.Length;
            while (size < length + extra) size *= 2;
            Array.Resize(ref buffer, size);
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            buffer[length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUShort(ushort value)
        {
            Ensure(2);
            buffer[length++] = (byte)value;
            buffer[length++] = (byte)(value >> 8);
        }

        public void WriteShort(short value) => WriteUShort((ushort)value);

        public void WriteInt(int value)
        {
            Ensure(4);
            buffer[length++] = (byte)value;
            buffer[length++] = (byte)(value >> 8);
            buffer[length++] = (byte)(value >> 16);
            buffer[length++] = (byte)(value >> 24);
        }

        public void WriteFloat(float value) => WriteInt(BitConverter.SingleToInt32Bits(value));

        public void WriteVector3(Vector3 value)
        {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        /// <summary>Cuaternión normalizado en 4 enteros de 16 bits (error &lt; 0,01°).</summary>
        public void WriteRotation(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude < 1e-6f) value = Quaternion.identity;
            else value = new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
            WriteShort(PackUnit(value.x));
            WriteShort(PackUnit(value.y));
            WriteShort(PackUnit(value.z));
            WriteShort(PackUnit(value.w));
        }

        static short PackUnit(float value) => (short)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * short.MaxValue);

        /// <summary>Texto UTF-8 con longitud de 1 byte (se recorta a 255 bytes).</summary>
        public void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
            int count = Mathf.Min(bytes.Length, 255);
            WriteByte((byte)count);
            Ensure(count);
            Array.Copy(bytes, 0, buffer, length, count);
            length += count;
        }
    }

    /// <summary>Lectura de los datos escritos por <see cref="NetWriter"/>. Nunca lanza: si faltan bytes devuelve ceros y marca <see cref="Failed"/>.</summary>
    public sealed class NetReader
    {
        byte[] buffer;
        int offset;
        int end;

        public NetReader() : this(Array.Empty<byte>(), 0, 0) { }

        public NetReader(byte[] data) : this(data, 0, data != null ? data.Length : 0) { }

        public NetReader(byte[] data, int start, int count)
        {
            Reset(data, start, count);
        }

        public void Reset(byte[] data, int start, int count)
        {
            buffer = data ?? Array.Empty<byte>();
            offset = start;
            end = Mathf.Min(buffer.Length, start + count);
            Failed = false;
        }

        public bool Failed { get; private set; }
        public int Remaining => end - offset;

        bool Has(int count)
        {
            if (offset + count <= end) return true;
            Failed = true;
            offset = end;
            return false;
        }

        public byte ReadByte() => Has(1) ? buffer[offset++] : (byte)0;

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUShort()
        {
            if (!Has(2)) return 0;
            ushort value = (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
            offset += 2;
            return value;
        }

        public short ReadShort() => (short)ReadUShort();

        public int ReadInt()
        {
            if (!Has(4)) return 0;
            int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);
            offset += 4;
            return value;
        }

        public float ReadFloat() => BitConverter.Int32BitsToSingle(ReadInt());

        public Vector3 ReadVector3() => new Vector3(ReadFloat(), ReadFloat(), ReadFloat());

        public Quaternion ReadRotation()
        {
            float x = ReadShort() / (float)short.MaxValue;
            float y = ReadShort() / (float)short.MaxValue;
            float z = ReadShort() / (float)short.MaxValue;
            float w = ReadShort() / (float)short.MaxValue;
            float magnitude = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            return magnitude < 1e-6f ? Quaternion.identity : new Quaternion(x / magnitude, y / magnitude, z / magnitude, w / magnitude);
        }

        public string ReadString()
        {
            int count = ReadByte();
            if (!Has(count)) return "";
            string value = Encoding.UTF8.GetString(buffer, offset, count);
            offset += count;
            return value;
        }
    }
}
