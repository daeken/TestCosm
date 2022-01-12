using System.Diagnostics;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS4014

namespace NetLib; 

public class Connection : IConnection {
	public Stream Stream;
	public readonly RemoteRoot RemoteRoot;
	public readonly List<string> Extensions = new();
	readonly Dictionary<ulong, IRemoteObject> RemoteObjects = new();
	readonly Dictionary<ulong, ILocalObject> LocalObjects = new();
	
	public Connection(Stream stream, Func<Connection, BaseRoot> rootGenerator) {
		Stream = stream;
		LocalObjects[0] = rootGenerator(this);
		RemoteObjects[0] = RemoteRoot = new RemoteRoot(this, 0);
	}

	public async Task Loop() {
		var sbuf = new byte[1];
		while(true) {
			await Stream.ReadAsync(sbuf);
			var compressionType = sbuf[0];
			Debug.Assert(compressionType == 0);
			var size = 0UL;
			var shift = 0;
			while(true) {
				await Stream.ReadAsync(sbuf);
				var bval = sbuf[0];
				size |= ((ulong) bval & 0x7F) << shift;
				shift += 7;
				if((bval & 0x80) == 0) break;
			}

			var buf = new byte[size];
			var offset = 0;
			var sequence = NetExtensions.DeserializeVu64(buf, ref offset);
			var commandNum = NetExtensions.DeserializeVi32(buf, ref offset);

			if((sequence & 1) == 0) { // Call
				var id = NetExtensions.DeserializeVu64(buf, ref offset);
				Console.WriteLine($"Call to object {id}, command {commandNum}");
				if(!LocalObjects.TryGetValue(id, out var obj))
					await Error(sequence, -1);
				else
					Task.Run(() => obj.HandleMessage(sequence, commandNum, buf, offset));
			} else { // Response
				Console.WriteLine("Got a response, woooo");
			}
		}
	}

	public async Task Handshake() {
		Task.Run(async () => {
			Extensions.AddRange(await RemoteRoot.ListExtensions());
			Console.WriteLine($"Got list of extensions! {string.Join(", ", Extensions)}");
		});
		await Loop();
	}

	public async Task<Memory<byte>> Call(ulong objectId, uint commandNumber, Memory<byte> buf) {
		throw new NotImplementedException();
	}
	public async Task Respond(ulong sequence, Memory<byte> buf) {
		if(sequence == 0) return;
		throw new NotImplementedException();
	}
	public async Task Error(ulong sequence, int err) {
		if(sequence == 0) return;
		throw new NotImplementedException();
	}
	public ulong RegisterLocalObject(Object obj) {
		throw new NotImplementedException();
	}
	public T GetObject<T>(ulong id, Func<ulong, T> generator) {
		throw new NotImplementedException();
	}
	public T GetCallback<T>(ulong id, Func<ulong, T> generator) {
		throw new NotImplementedException();
	}
	public ulong GetCallbackId<T>(T callback, Func<Func<ulong, Memory<byte>, Task>> generator = null) {
		throw new NotImplementedException();
	}
}