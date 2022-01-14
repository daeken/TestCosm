using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NetLib; 

public struct Uuid {
	static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
	public readonly ulong Piece1, Piece2;

	public Uuid(Span<byte> data) {
		Debug.Assert(data.Length >= 16);
		var p = MemoryMarshal.Cast<byte, ulong>(data);
		Piece1 = p[0];
		Piece2 = p[1];
	}

	public Uuid(ulong piece1, ulong piece2) {
		Piece1 = piece1;
		Piece2 = piece2;
	}

	public static Uuid Generate() {
		Span<byte> b = stackalloc byte[16];
		Rng.GetBytes(b);
		return new(b);
	}

	public void GetBytes(Span<byte> data) {
		BitConverter.GetBytes(Piece1).CopyTo(data);
		BitConverter.GetBytes(Piece2).CopyTo(data[8..]);
	}

	public void Serialize(Span<byte> buf, ref int offset) {
		NetExtensions.SerializeU64(Piece1, buf, ref offset);
		NetExtensions.SerializeU64(Piece2, buf, ref offset);
	}

	public static Uuid Deserialize(Span<byte> buf, ref int offset) => new(
		NetExtensions.DeserializeU64(buf, ref offset), 
		NetExtensions.DeserializeU64(buf, ref offset)
	);

	public override bool Equals(object obj) => obj is Uuid other && Piece1 == other.Piece1 && Piece2 == other.Piece2;
	public override int GetHashCode() => HashCode.Combine(Piece1.GetHashCode(), Piece2.GetHashCode());
}