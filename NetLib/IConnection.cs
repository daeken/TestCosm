namespace NetLib; 

public interface IConnection {
	Task<Memory<byte>> Call(ulong objectId, uint commandNumber, Memory<byte> buf);
	Task Respond(ulong sequence, Memory<byte> buf);
	Task Error(ulong sequence, int err);
	ulong RegisterLocalObject(Generated.Object obj);
	T GetObject<T>(ulong id, Func<ulong, T> generator);
	T GetCallback<T>(ulong id, Func<ulong, T> generator);
	ulong GetCallbackId<T>(T callback, Func<Func<ulong, Memory<byte>, Task>> generator = null);
}