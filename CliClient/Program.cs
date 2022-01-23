using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using CliClient;
using NetLib;
using NetLib.Generated;
using ExecutionContext = NetLib.Generated.ExecutionContext;
using Object = NetLib.Generated.Object;
#pragma warning disable CS1998

class ClientRoot : BaseRoot {
	public ClientRoot(IConnection connection) : base(connection) {}
	public override async Task<string[]> ListExtensions() {
		return new[] { "hypercosm.assetdelivery.v0.1.0", "hypercosm.world.v0.1.0", "hypercosm.execution_context.v.0.1" };
	}
	public override async Task Ping() {
	}
	public override Task<Object> GetObjectById(Uuid id) {
		throw new NotImplementedException();
	}
	public override async Task<Object> GetObjectByName(string name) =>
		name switch {
			ExecutionContext._ProtocolName => new ClientExecutionContext(Connection), 
			_ => throw new CommandException(1)
		};
}

public static class Program {
	public static AssetDelivery AssetDelivery;
	
	public static async Task Main() {
		var tclient = new TcpClient("localhost", 12345);
		var stream = new SslStream(tclient.GetStream(), false, (_, _, _, _) => true);
		await stream.AuthenticateAsClientAsync("");

		Memory<byte> skb = new byte[16];
		Uuid.Generate().GetBytes(skb.Span);
		await stream.WriteAsync(skb);
		await stream.ReadAllAsync(skb);

		var conn = new Connection(stream, conn => new ClientRoot(conn));
		await conn.Handshake();

		AssetDelivery = await conn.GetObjectFromRoot<AssetDelivery>();

		await Task.Delay(1000000000);
	}
}
