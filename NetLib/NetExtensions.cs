using System.Numerics;
using System.Text;
using NetLib.Generated;

namespace NetLib; 

public static partial class NetExtensions {
	public static void SerializeU8(byte val, Span<byte> buf, ref int offset) {
		if(offset >= buf.Length) throw new SerializationException();
		buf[offset++] = val;
	}
	public static byte DeserializeU8(Span<byte> buf, ref int offset) {
		if(offset >= buf.Length) throw new SerializationException();
		return buf[offset++];
	}
	public static int SizeU8(byte _) => 1;

	public static void SerializeU64(ulong val, Span<byte> buf, ref int offset) {
		if(offset + 8 > buf.Length) throw new SerializationException();
		BitConverter.GetBytes(val).CopyTo(buf[offset..]);
		offset += 8;
	}
	public static ulong DeserializeU64(Span<byte> buf, ref int offset) {
		if(offset + 8 > buf.Length) throw new SerializationException();
		var val = BitConverter.ToUInt64(buf[offset..(offset+8)]);
		offset += 8;
		return val;
	}
	public static int SizeU64(ulong _) => 8;

	public static void SerializeVi32(int val, Span<byte> buf, ref int offset) {
		var more = true;
		while(more) {
			if(offset >= buf.Length) throw new SerializationException();
			var bval = val & 0x7F;
			var sbit = (bval & 0x40) != 0;
			val >>= 7;
			more = !((val == 0 && !sbit) || (val == -1 && sbit));
			if(more)
				bval |= 0x80;
			buf[offset++] = (byte) bval;
		}
	}
	public static int DeserializeVi32(Span<byte> buf, ref int offset) {
		var value = 0;
		var shift = 0;
		bool more = true, signBitSet = false;

		while(more) {
			if(offset >= buf.Length) throw new SerializationException();
			var bval = buf[offset++];

			more = (bval & 0x80) != 0; // extract msb
			signBitSet = (bval & 0x40) != 0; // sign bit is the msb of a 7-bit byte, so 0x40

			var chunk = (sbyte) bval & 0x7f; // extract lower 7 bits
			value |= chunk << shift;
			shift += 7;
		};

		// extend the sign of shorter negative numbers
		if (shift < 32 && signBitSet) { value |= -1 << shift; }

		return value;
	}
	public static int SizeVi32(int val) {
		var more = true;
		var count = 0;
		while(more) {
			var bval = val & 0x7F;
			var sbit = (bval & 0x40) != 0;
			val >>= 7;
			more = !((val == 0 && !sbit) || (val == -1 && sbit));
			count++;
		}
		return count;
	}

	public static void SerializeVu64(ulong val, Span<byte> buf, ref int offset) {
		do {
			if(offset >= buf.Length) throw new SerializationException();
			var bval = val & 0x7F;
			val >>= 7;
			if(val > 0)
				bval |= 0x80;
			buf[offset++] = (byte) bval;
		} while(val != 0);
	}
	public static ulong DeserializeVu64(Span<byte> buf, ref int offset) {
		var val = 0UL;
		var shift = 0;
		while(true) {
			if(offset >= buf.Length) throw new SerializationException();
			var bval = buf[offset++];
			val |= ((ulong) bval & 0x7F) << shift;
			shift += 7;
			if((bval & 0x80) == 0) break;
		}
		return val;
	}
	public static int SizeVu64(ulong val) {
		var count = 0;
		do {
			count++;
			val >>= 7;
		} while(val > 0);
		return count;
	}

	public static void SerializeF32(float val, Span<byte> buf, ref int offset) {
		if(offset + 4 > buf.Length) throw new SerializationException();
		BitConverter.GetBytes(val).CopyTo(buf[offset..]);
		offset += 4;
	}
	public static float DeserializeF32(Span<byte> buf, ref int offset) {
		if(offset + 4 > buf.Length) throw new SerializationException();
		var val = BitConverter.ToSingle(buf[offset..(offset+4)]);
		offset += 4;
		return val;
	}
	public static int SizeF32(float _) => 4;

	public static void SerializeBytes(byte[] val, Span<byte> buf, ref int offset) {
		SerializeVu64((ulong) val.Length, buf, ref offset);
		if(offset + val.Length > buf.Length) throw new SerializationException();
		val.CopyTo(buf[offset..(offset + val.Length)]);
		offset += val.Length;
	}
	public static byte[] DeserializeBytes(Span<byte> buf, ref int offset) {
		var len = (int) DeserializeVu64(buf, ref offset);
		if(offset + len > buf.Length) throw new SerializationException();
		var val = buf[offset..(offset + len)].ToArray();
		offset += len;
		return val;
	}
	public static int SizeBytes(byte[] val) => SizeVu64((ulong) val.Length) + val.Length;

	public static void SerializeString(string val, Span<byte> buf, ref int offset) {
		var bytes = Encoding.UTF8.GetBytes(val);
		SerializeVu64((ulong) bytes.Length, buf, ref offset);
		if(offset + bytes.Length > buf.Length) throw new SerializationException();
		bytes.CopyTo(buf[offset..(offset + bytes.Length)]);
		offset += bytes.Length;
	}
	public static string DeserializeString(Span<byte> buf, ref int offset) {
		var len = (int) DeserializeVu64(buf, ref offset);
		if(offset + len > buf.Length) throw new SerializationException();
		var bytes = buf[offset..(offset + len)];
		offset += len;
		return Encoding.UTF8.GetString(bytes);
	}
	public static int SizeString(string val) {
		var bytes = Encoding.UTF8.GetBytes(val);
		return SizeVu64((ulong) bytes.Length) + bytes.Length;
	}

	public static void SerializeMatrix4x4(Matrix4x4 val, Span<byte> buf, ref int offset) {
		SerializeF32(val.M11, buf, ref offset);
		SerializeF32(val.M12, buf, ref offset);
		SerializeF32(val.M13, buf, ref offset);
		SerializeF32(val.M14, buf, ref offset);
		
		SerializeF32(val.M21, buf, ref offset);
		SerializeF32(val.M22, buf, ref offset);
		SerializeF32(val.M23, buf, ref offset);
		SerializeF32(val.M24, buf, ref offset);
		
		SerializeF32(val.M31, buf, ref offset);
		SerializeF32(val.M32, buf, ref offset);
		SerializeF32(val.M33, buf, ref offset);
		SerializeF32(val.M34, buf, ref offset);
		
		SerializeF32(val.M41, buf, ref offset);
		SerializeF32(val.M42, buf, ref offset);
		SerializeF32(val.M43, buf, ref offset);
		SerializeF32(val.M44, buf, ref offset);
	}
	public static Matrix4x4 DeserializeMatrix4x4(Span<byte> buf, ref int offset) =>
		new(
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 

			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset), 
			DeserializeF32(buf, ref offset)
		);
	public static int SizeMatrix4x4(Matrix4x4 _) => 4 * 4 * 4;

	public static async Task ReadAllAsync(this Stream stream, Memory<byte> buf) {
		while(buf.Length != 0) {
			var count = await stream.ReadAsync(buf);
			if(count == 0) throw new DisconnectedException();
			if(count == buf.Length) break;
			buf = buf[count..];
		}
	}
}