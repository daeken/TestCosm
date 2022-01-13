using NetLib;
using NetLib.Generated;

namespace TestCosm; 

public class ServerWorld : BaseWorld {
	public ServerWorld(IConnection connection) : base(connection) {}
	public override async Task<string[]> ListInterfaces() => new[] { "hypercosm.object.v1.0.0", "hypercosm.world.v0.1.0" };
	public override Task Release() {
		throw new NotImplementedException();
	}
	public override Task SubscribeAddEntities(Func<EntityInfo[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task UnsubscribeAddEntities(Func<EntityInfo[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task SubscribeUpdateEntities(Func<EntityInfo[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task UnsubscribeUpdateEntities(Func<EntityInfo[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task SubscribeRemoveEntities(Func<Entity[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task UnsubscribeRemoveEntities(Func<Entity[], Task> callback) {
		throw new NotImplementedException();
	}
}