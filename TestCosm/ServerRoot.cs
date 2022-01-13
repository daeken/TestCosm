using NetLib;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS1998

namespace TestCosm; 

public class ServerRoot : BaseRoot {
	public ServerRoot(IConnection connection) : base(connection) {}
	public override async Task<string[]> ListInterfaces() {
		return new[] { "hypercosm.object.v1.0.0", "hypercosm.root.v0.1.0" };
	}
	public override async Task Release() {
		Console.WriteLine("Attempted release of Root.");
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