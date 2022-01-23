using NetLib;
using NetLib.Generated;
using Object = NetLib.Generated.Object;
#pragma warning disable CS1998

namespace TestCosm; 

public class ServerRoot : BaseRoot {
	readonly Dictionary<string, Object> NamedObjects = new();
	public readonly ServerAssetDelivery AssetDelivery;
	public readonly ServerWorld World;

	public ServerRoot(IConnection connection) : base(connection) {
		AssetDelivery = new ServerAssetDelivery(connection);
		World = new ServerWorld(connection, AssetDelivery);
	}
	public override Task<string[]> ListExtensions() =>
		Task.FromResult(new[] {
			NetLib.Generated.AssetDelivery._ProtocolName,
			NetLib.Generated.World._ProtocolName,
			NetLib.Generated.ExecutionContext._ProtocolName
		});
	public override async Task Ping() {}
	public override Task<Object> GetObjectById(Uuid id) {
		throw new CommandException(1);
	}
	public override async Task<Object> GetObjectByName(string name) {
		if(NamedObjects.TryGetValue(name, out var obj)) return obj;
		return NamedObjects[name] = name switch {
			NetLib.Generated.AssetDelivery._ProtocolName => AssetDelivery, 
			NetLib.Generated.World._ProtocolName => World, 
			_ => throw new CommandException(1)
		};
	}
}