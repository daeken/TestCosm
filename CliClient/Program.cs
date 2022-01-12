using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NetLib;
using NetLib.Generated;
using Object = NetLib.Generated.Object;

var tclient = new TcpClient("localhost", 12345);
var stream = new SslStream(tclient.GetStream(), false, (_, _, _, _) => true);
await stream.AuthenticateAsClientAsync("");

Memory<byte> skb = new byte[16];
Uuid.Generate().GetBytes(skb.Span);
await stream.WriteAsync(skb);
await stream.ReadAsync(skb);
var nuid = new Uuid(skb.Span);

var conn = new Connection(stream, conn => new ClientRoot(conn));
await conn.Handshake();

class ClientRoot : BaseRoot {
	public ClientRoot(IConnection connection) : base(connection) {}
	public override Task<string[]> ListInterfaces() {
		throw new NotImplementedException();
	}
	public override Task Release() {
		throw new NotImplementedException();
	}
	public override Task<string[]> ListExtensions() {
		throw new NotImplementedException();
	}
	public override Task Ping() {
		throw new NotImplementedException();
	}
	public override Task<Object> GetObjectById(Uuid id) {
		throw new NotImplementedException();
	}
	public override Task<Object> GetObjectByName(string name) {
		throw new NotImplementedException();
	}
}
