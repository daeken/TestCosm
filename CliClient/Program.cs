using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NetLib;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS1998

var tclient = new TcpClient("localhost", 12345);
var stream = new SslStream(tclient.GetStream(), false, (_, _, _, _) => true);
await stream.AuthenticateAsClientAsync("");

Memory<byte> skb = new byte[16];
Uuid.Generate().GetBytes(skb.Span);
await stream.WriteAsync(skb);
await stream.ReadAllAsync(skb);
var nuid = new Uuid(skb.Span);

var conn = new Connection(stream, conn => new ClientRoot(conn));
await conn.Handshake();

class ClientRoot : BaseRoot {
	public ClientRoot(IConnection connection) : base(connection) {}
	public override async Task<string[]> ListInterfaces() {
		return new[] { "hypercosm.object.v1.0.0", "hypercosm.root.v0.1.0" };
	}
	public override async Task Release() {
	}
	public override async Task<string[]> ListExtensions() {
		return new[] { "hypercosm.assetdelivery.v0.1.0", "hypercosm.world.v0.1.0" };
	}
	public override async Task Ping() {
	}
	public override Task<Object> GetObjectById(Uuid id) {
		throw new NotImplementedException();
	}
	public override Task<Object> GetObjectByName(string name) {
		throw new NotImplementedException();
	}
}
