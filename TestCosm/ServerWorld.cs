using System.Numerics;
using NetLib;
using NetLib.Generated;
#pragma warning disable CS1998

namespace TestCosm;

class NoopEntity : BaseEntity {
	public NoopEntity(IConnection connection) : base(connection) {}
	public override Task<string[]> ListInterfaces() => throw new NotImplementedException();
	public override Task Release() => throw new NotImplementedException();
	public override Task Interact() => throw new NotImplementedException();
}

public class ServerWorld : BaseWorld {
	readonly ServerAssetDelivery AssetDelivery;
	bool ServedEntities;

	public ServerWorld(IConnection connection, ServerAssetDelivery assetDelivery) : base(connection) =>
		AssetDelivery = assetDelivery;
	public override async Task<string[]> ListInterfaces() => new[] { "hypercosm.object.v1.0.0", "hypercosm.world.v0.1.0" };
	public override Task Release() {
		throw new NotImplementedException();
	}
	public override async Task SubscribeAddEntities(Func<EntityInfo[], Task> callback) {
		if(ServedEntities) return;

		var roomEntity = new EntityInfo {
			AssetId = await AssetDelivery.GetId("Room/scene.glb"), 
			Entity = new NoopEntity(Connection), 
			Flags = EntityFlags.Collidable, 
			Transformation = Matrix4x4.CreateScale(0.1f) * Matrix4x4.CreateTranslation(-47.1f, 0, 46.67f)
		};
		var mechEntity = new EntityInfo {
			AssetId = await AssetDelivery.GetId("mech.glb"), 
			Entity = new NoopEntity(Connection), 
			Flags = EntityFlags.Collidable, 
			Transformation = Matrix4x4.CreateScale(2.5f) * Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 382.21f, 330.63f)
		};
		var droidEntity = new EntityInfo {
			AssetId = await AssetDelivery.GetId("droid.glb"), 
			Entity = new NoopEntity(Connection), 
			Flags = EntityFlags.Collidable, 
			Transformation = Matrix4x4.CreateScale(0.03f) * Matrix4x4.CreateRotationY(-MathF.PI / 2) * Matrix4x4.CreateTranslation(-66.62f, 0, 7.69f)
		};
		await callback(new[] { roomEntity, mechEntity, droidEntity });

		ServedEntities = true;
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