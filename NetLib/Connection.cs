using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS4014

namespace NetLib;

class CallbackObject : BaseObject {
	readonly Func<ulong, Memory<byte>, Task> Callback;

	public CallbackObject(IConnection connection, Func<ulong, Memory<byte>, Task> callback) : base(connection) =>
		Callback = callback;

	public override Task<string[]> ListInterfaces() => Task.FromResult(new[] { "hypercosm.object.v1.0.0", "hypercosm.callback.v1.0.0" });
	public override async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {
		switch(commandNumber) {
			case 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;
			case 2:
				await Callback(sequence, buf[offset..]);
				break;
			default:
				throw new UnknownCommandException();
		}
	}
}

public class Connection : IConnection {
	public Stream Stream;
	public readonly RemoteRoot RemoteRoot;
	public readonly List<string> Extensions = new();
	readonly ConcurrentDictionary<ulong, IRemoteObject> RemoteObjects = new();
	readonly ConcurrentDictionary<ulong, ILocalObject> LocalObjects = new();
	readonly ConcurrentDictionary<ulong, TaskCompletionSource<Memory<byte>>> ResponseWaiters = new();
	readonly ConcurrentDictionary<object, ulong> LocalCallbackIds = new();
	readonly ConcurrentDictionary<ulong, object> RemoteCallbacks = new();
	ulong LocalObjectI;
	readonly SemaphoreSlim SendSemaphore = new(1, 1);
	ulong Sequence;
	readonly Action<string> Log;
	
	public Connection(Stream stream, Func<Connection, BaseRoot> rootGenerator, Action<string> log = null) {
		Stream = stream;
		rootGenerator(this);
		Debug.Assert(LocalObjects.Count >= 1);
		RemoteObjects[0] = RemoteRoot = new RemoteRoot(this, 0);

		Log = log ?? Console.WriteLine;
	}

	public async Task Loop() {
		var sbuf = new byte[1];
		while(true) {
			await Stream.ReadAllAsync(sbuf);
			var compressionType = sbuf[0];
			Log($"Got start of message with compression type {compressionType}!");
			if(compressionType != 0) throw new DisconnectedException();
			var size = 0UL;
			var shift = 0;
			Log("Reading message length...");
			while(true) {
				await Stream.ReadAllAsync(sbuf);
				Log($"Message length byte 0x{sbuf[0]:X02}");
				var bval = sbuf[0];
				size |= ((ulong) bval & 0x7F) << shift;
				shift += 7;
				if((bval & 0x80) == 0) break;
			}
			
			Log($"Getting message buffer of length {size}");

			var buf = new byte[size];
			await Stream.ReadAllAsync(buf);
			//Log($"Message bytes: {string.Join(", ", buf.Select(x => $"{x:x02}"))}");
			var offset = 0;
			var sequence = NetExtensions.DeserializeVu64(buf, ref offset);
			var commandNum = NetExtensions.DeserializeVi32(buf, ref offset);
			
			Log($"Sequence {sequence} commandNum {commandNum}");

			if((sequence & 1) == 0) { // Call
				var id = NetExtensions.DeserializeVu64(buf, ref offset);
				Log($"Call to object {id}, command {commandNum}");
				if(!LocalObjects.TryGetValue(id, out var obj))
					await Error(sequence, -1);
				else
					await Task.Factory.StartNew(async () => {
						try {
							await obj.HandleMessage(sequence, commandNum, buf, offset);
						} catch(CommandException ce) {
							await Error(sequence, ce.Error);
						} catch(Exception e) {
							Log($"Exception in handling command {commandNum} to object {id} ({obj}): {e}");
						}
					});
			} else { // Response
				Log($"Got a response with sequence {sequence}");
				if(ResponseWaiters.TryRemove(sequence, out var waiter)) {
					if(commandNum == 0) {
						Log("Sending buffer to waiting call...");
						waiter.SetResult(buf.AsMemory()[offset..]);
					} else {
						Log("Sending error back to waiting call");
						waiter.SetException(new CommandException(commandNum));
					}
				} else
					Log("Response for unknown sequence!");
			}
		}
	}

	async Task SendMessage(Memory<byte> buf) {
		Log("Attempting to get semaphore");
		await SendSemaphore.WaitAsync();
		Log("Got semaphore");
		Memory<byte> mbuf = new byte[1 + NetExtensions.SizeVu64((ulong) buf.Length)];
		mbuf.Span[0] = 0; // No compression
		var offset = 1;
		NetExtensions.SerializeVu64((ulong) buf.Length, mbuf.Span, ref offset);
		Log($"Sending full message buffer of length {mbuf.Length + buf.Length}");
		//Log($"Message bytes being sent: {string.Join(", ", mbuf.ToArray().Select(x => $"{x:x02}"))}    {string.Join(", ", buf.ToArray().Select(x => $"{x:x02}"))}");
		await Stream.WriteAsync(mbuf);
		await Stream.WriteAsync(buf);
		Log($"Sent message!");
		SendSemaphore.Release();
	}

	public async Task Handshake() {
		Task.Factory.StartNew(Loop);
		try {
			Log("Going to request extensions...");
			Extensions.AddRange(await RemoteRoot.ListExtensions());
			Log($"Got list of extensions! {string.Join(", ", Extensions)}");
		} catch(Exception e) {
			Log($"Exception in handshake: {e}");
		}
	}

	public async Task<Memory<byte>> Call(ulong objectId, uint commandNumber, Memory<byte> buf) {
		ulong sequence;
		lock(this) {
			sequence = Sequence += 2;
		}
		Log($"Sending call with sequence {sequence}, objectId {objectId}, command number {commandNumber}");
		//Log($"Call body: {string.Join(", ", buf.ToArray().Select(x => $"{x:x02}"))}");
		Memory<byte> tbuf = new byte[NetExtensions.SizeVu64(sequence) + NetExtensions.SizeVi32((int) commandNumber) + NetExtensions.SizeVu64(objectId) + buf.Length];
		var offset = 0;
		NetExtensions.SerializeVu64(sequence, tbuf.Span, ref offset);
		NetExtensions.SerializeVi32((int) commandNumber, tbuf.Span, ref offset);
		NetExtensions.SerializeVu64(objectId, tbuf.Span, ref offset);
		buf.CopyTo(tbuf[offset..]);
		var tcs = ResponseWaiters[sequence | 1] = new();
		await SendMessage(tbuf);
		var rbuf = await tcs.Task;
		Log("Got response buffer!");
		return rbuf;
	}

	async Task Respond(ulong sequence, int commandNumber, Memory<byte> buf) {
		sequence |= 1;
		Log($"Sending response with sequence {sequence}, command number {commandNumber}, and {buf.Length} bytes of data");
		Memory<byte> tbuf = new byte[NetExtensions.SizeVu64(sequence) + NetExtensions.SizeVi32(commandNumber) + buf.Length];
		var offset = 0;
		NetExtensions.SerializeVu64(sequence, tbuf.Span, ref offset);
		NetExtensions.SerializeVi32(commandNumber, tbuf.Span, ref offset);
		buf.CopyTo(tbuf[offset..]);
		await SendMessage(tbuf);
	}
	
	public async Task Respond(ulong sequence, Memory<byte> buf) {
		if(sequence == 0) return;
		await Respond(sequence, 0, buf);
	}
	public async Task Error(ulong sequence, int err) {
		if(sequence == 0) return;
		await Respond(sequence, err, Memory<byte>.Empty);
	}
	public ulong RegisterLocalObject(ILocalObject obj) {
		lock(this) {
			LocalObjects[LocalObjectI] = obj;
			return LocalObjectI++;
		}
	}
	public T GetLocalObject<T>(ulong id) where T : BaseObject => LocalObjects.TryGetValue(id, out var obj) ? obj as T : null;
	public T GetObject<T>(ulong id, Func<ulong, T> generator) {
		if(RemoteObjects.TryGetValue(id, out var obj) && obj is T tobj)
			return tobj;
		return (T) (RemoteObjects[id] = (IRemoteObject) generator(id));
	}

	public async Task<T> GetObjectFromRoot<T>() where T : Object {
		var iname = (string) typeof(T).GetField("_ProtocolName", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) ?? throw new Exception();
		var gobj = await RemoteRoot.GetObjectByName(iname);
		var rtype = (Type) typeof(T).GetField("_RemoteType", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ?? throw new Exception();
		return (T) Activator.CreateInstance(rtype, this, gobj.ObjectId);
	}

	public T GetCallback<T>(ulong id, Func<ulong, T> generator) {
		if(RemoteCallbacks.TryGetValue(id, out var cb)) return (T) cb;
		return (T) (RemoteCallbacks[id] = generator(id));
	}
	public ulong GetCallbackId<T>(T callback, Func<Func<ulong, Memory<byte>, Task>> generator = null) {
		var found = LocalCallbackIds.TryGetValue(callback, out var id);
		if(found && (generator == null || LocalObjects[id] != null)) return id;
		if(generator == null) return LocalCallbackIds[callback] = RegisterLocalObject(null);
		var cbo = new CallbackObject(this, generator());
		if(!found) return RegisterLocalObject(cbo);
		LocalObjects[id] = cbo;
		return id;
	}
	public Task Release(ulong id) {
		throw new NotImplementedException();
	}
}