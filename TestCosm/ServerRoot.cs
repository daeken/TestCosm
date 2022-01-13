using NetLib;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS1998

namespace TestCosm; 

public class ServerRoot : BaseRoot {
	readonly Dictionary<string, Object> NamedObjects = new();
	
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
		throw new CommandException(1);
	}
	public override async Task<Object> GetObjectByName(string name) {
		if(NamedObjects.TryGetValue(name, out var obj)) return obj;
		return NamedObjects[name] = name switch {
			"hypercosm.assetdelivery.v0.1.0" => new ServerAssetDelivery(Connection), 
			"hypercosm.world.v0.1.0" => new ServerWorld(Connection), 
			_ => throw new CommandException(1)
		};
	}
}