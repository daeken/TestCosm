using System.Security.Cryptography;
using NetLib;
using NetLib.Generated;
#pragma warning disable CS1998

namespace TestCosm; 

public class ServerAssetDelivery : BaseAssetdelivery {
	static readonly Dictionary<Uuid, byte[]> Assets = new();
	static readonly Dictionary<string, Uuid> NameToIdMap = new();
	static readonly Dictionary<Uuid, string> IdToNameMap = new();
	static ServerAssetDelivery() {
		foreach(var fn in Directory.EnumerateFiles("Assets/", "*.*", SearchOption.AllDirectories)) {
			var data = File.ReadAllBytes(fn);
			var uuid = new Uuid(SHA256.HashData(data));
			Assets[uuid] = data;
			NameToIdMap[fn[7..]] = uuid;
			IdToNameMap[uuid] = fn[7..];
		}
	}

	public static void EnsureLoaded() {}
	
	public ServerAssetDelivery(IConnection connection) : base(connection) {}
	public override Task<string[]> ListInterfaces() {
		throw new NotImplementedException();
	}
	public override Task Release() {
		throw new NotImplementedException();
	}
	public override Task SubscribeLoadAssets(Func<Asset[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task UnsubscribeLoadAssets(Func<Asset[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task SubscribeUnloadAssets(Func<Uuid[], Task> callback) {
		throw new NotImplementedException();
	}
	public override Task UnsubscribeUnloadAssets(Func<Uuid[], Task> callback) {
		throw new NotImplementedException();
	}
	public override async Task<Asset> FetchAssetById(Uuid id) {
		if(!Assets.TryGetValue(id, out var data)) throw new CommandException(1);
		return new Asset {
			Data = data, 
			Id = id, 
			Name = IdToNameMap[id]
		};
	}
	public override async Task<Asset> FetchAssetByName(string name) {
		if(!NameToIdMap.TryGetValue(name, out var uuid)) throw new CommandException(1);
		return new Asset {
			Data = Assets[uuid], 
			Id = uuid, 
			Name = name
		};
	}
	public override Task<Asset[]> FetchAssetsByIds(Uuid[] ids) {
		throw new NotImplementedException();
	}
	public override Task<Asset[]> FetchAssetsByNames(string[] names) {
		throw new NotImplementedException();
	}
	public override async Task<Uuid> GetId(string name) {
		if(!NameToIdMap.TryGetValue(name, out var uuid)) throw new CommandException(1);
		return uuid;
	}
}