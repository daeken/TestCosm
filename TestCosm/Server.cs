using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NetLib;

namespace TestCosm; 

public class Server {
	readonly TcpListener Listener;
	readonly Dictionary<Uuid, Connection> Clients = new();

	public Server() {
		Listener = TcpListener.Create(12345);
		Listener.Start(100);
	}

	public void Run() {
		while(true) {
			Console.WriteLine("Waiting for connection...");
			var client = Listener.AcceptTcpClient();
			Console.WriteLine($"Connection from {client.Client.RemoteEndPoint}");
			Task.Run(() => ClientLoop(client));
		}
	}

	async Task ClientLoop(TcpClient client) {
		try {
			var stream = new SslStream(client.GetStream());
			await stream.AuthenticateAsServerAsync(new X509Certificate2("cert.pfx", "testing"), false, SslProtocols.Tls13 | SslProtocols.Tls12, false);
			Console.WriteLine("TLS set up");
			Memory<byte> skb = new byte[16];
			await stream.ReadAllAsync(skb);
			var sessionKey = new Uuid(skb.Span);
			if(Clients.TryGetValue(sessionKey, out var cl)) {
				await stream.WriteAsync(skb);
				cl.Stream = stream;
				await cl.Loop();
			} else {
				sessionKey = Uuid.Generate();
				sessionKey.GetBytes(skb.Span);
				await stream.WriteAsync(skb);
				Clients[sessionKey] = cl = new Connection(stream, conn => new ServerRoot(conn));
				await cl.Handshake();
			}
		} catch(Exception e) {
			Console.WriteLine($"Something broke? {e}");
		}
	}
}