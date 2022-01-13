using System.Collections.Concurrent;
using System.Diagnostics;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS4014

namespace NetLib; 

public class Connection : IConnection {
	public Stream Stream;
	public readonly RemoteRoot RemoteRoot;
	public readonly List<string> Extensions = new();
	readonly ConcurrentDictionary<ulong, IRemoteObject> RemoteObjects = new();
	readonly ConcurrentDictionary<ulong, ILocalObject> LocalObjects = new();
	readonly ConcurrentDictionary<ulong, TaskCompletionSource<Memory<byte>>> ResponseWaiters = new();
	ulong LocalObjectI;
	readonly SemaphoreSlim SendSemaphore = new(1, 1);
	ulong Sequence = 0;
	
	public Connection(Stream stream, Func<Connection, BaseRoot> rootGenerator) {
		Stream = stream;
		LocalObjects[0] = rootGenerator(this);
		RemoteObjects[0] = RemoteRoot = new RemoteRoot(this, 0);
	}

	public async Task Loop() {
		var sbuf = new byte[1];
		while(true) {
			await Stream.ReadAllAsync(sbuf);
			var compressionType = sbuf[0];
			Console.WriteLine($"Got start of message with compression type {compressionType}!");
			if(compressionType != 0) throw new DisconnectedException();
			var size = 0UL;
			var shift = 0;
			Console.WriteLine("Reading message length...");
			while(true) {
				await Stream.ReadAllAsync(sbuf);
				Console.WriteLine($"Message length byte 0x{sbuf[0]:X02}");
				var bval = sbuf[0];
				size |= ((ulong) bval & 0x7F) << shift;
				shift += 7;
				if((bval & 0x80) == 0) break;
			}
			
			Console.WriteLine($"Getting message buffer of length {size}");

			var buf = new byte[size];
			await Stream.ReadAllAsync(buf);
			Console.WriteLine($"Message bytes: {string.Join(", ", buf.Select(x => $"{x:x02}"))}");
			var offset = 0;
			var sequence = NetExtensions.DeserializeVu64(buf, ref offset);
			var commandNum = NetExtensions.DeserializeVi32(buf, ref offset);
			
			Console.WriteLine($"Sequence {sequence} commandNum {commandNum}");

			if((sequence & 1) == 0) { // Call
				var id = NetExtensions.DeserializeVu64(buf, ref offset);
				Console.WriteLine($"Call to object {id}, command {commandNum}");
				if(!LocalObjects.TryGetValue(id, out var obj))
					await Error(sequence, -1);
				else
					await Task.Factory.StartNew(async () => {
						try {
							await obj.HandleMessage(sequence, commandNum, buf, offset);
						} catch(Exception e) {
							Console.WriteLine($"Exceptiong in handling command {commandNum} to object {id} ({obj}): {e}");
						}
					});
			} else { // Response
				Console.WriteLine("Got a response with sequence {sequence}");
				if(ResponseWaiters.TryRemove(sequence, out var waiter)) {
					Console.WriteLine($"Sending buffer to waiting call...");
					waiter.SetResult(buf.AsMemory()[offset..]);
				} else
					Console.WriteLine("Response for unknown sequence!");
			}
		}
	}

	async Task SendMessage(Memory<byte> buf) {
		Console.WriteLine("Attempting to get semaphore");
		await SendSemaphore.WaitAsync();
		Console.WriteLine("Got semaphore");
		Memory<byte> mbuf = new byte[1 + NetExtensions.SizeVu64((ulong) buf.Length)];
		mbuf.Span[0] = 0; // No compression
		var offset = 1;
		NetExtensions.SerializeVu64((ulong) buf.Length, mbuf.Span, ref offset);
		Console.WriteLine($"Sending full message buffer of length {mbuf.Length + buf.Length}");
		Console.WriteLine($"Message bytes being sent: {string.Join(", ", mbuf.ToArray().Select(x => $"{x:x02}"))}    {string.Join(", ", buf.ToArray().Select(x => $"{x:x02}"))}");
		await Stream.WriteAsync(mbuf);
		await Stream.WriteAsync(buf);
		Console.WriteLine($"Sent message!");
		SendSemaphore.Release();
	}

	public async Task Handshake() {
		var hstask = await Task.Factory.StartNew(async () => {
			try {
				Console.WriteLine("Going to request extensions...");
				Extensions.AddRange(await RemoteRoot.ListExtensions());
				Console.WriteLine($"Got list of extensions! {string.Join(", ", Extensions)}");
			} catch(Exception e) {
				Console.WriteLine($"Exception in handshake: {e}");
			}
		});
		var looptask = await Task.Factory.StartNew(Loop);
		await Task.WhenAll(hstask, looptask);
	}

	public async Task<Memory<byte>> Call(ulong objectId, uint commandNumber, Memory<byte> buf) {
		var sequence = Interlocked.Add(ref Sequence, 2);
		Console.WriteLine($"Sending call with sequence {sequence}, objectId {objectId}, command number {commandNumber}");
		Memory<byte> tbuf = new byte[NetExtensions.SizeVu64(sequence) + NetExtensions.SizeVi32((int) commandNumber) + NetExtensions.SizeVu64(objectId) + buf.Length];
		var offset = 0;
		NetExtensions.SerializeVu64(sequence, tbuf.Span, ref offset);
		NetExtensions.SerializeVi32((int) commandNumber, tbuf.Span, ref offset);
		NetExtensions.SerializeVu64(objectId, tbuf.Span, ref offset);
		buf.CopyTo(tbuf[offset..]);
		await SendMessage(tbuf);
		var tcs = ResponseWaiters[sequence | 1] = new();
		var rbuf = await tcs.Task;
		Console.WriteLine("Got response buffer!");
		return rbuf;
	}

	async Task Respond(ulong sequence, int commandNumber, Memory<byte> buf) {
		sequence |= 1;
		Console.WriteLine($"Sending response with sequence {sequence}, command number {commandNumber}, and {buf.Length} bytes of data");
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
		LocalObjects[LocalObjectI] = obj;
		return LocalObjectI++;
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