using System.Security.Cryptography;
using NetLib;
using NetLib.Generated;

namespace TestCosm; 

public class ServerAssetDelivery : BaseAssetdelivery {
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
	public override Task<Asset> FetchAssetById(Uuid id) {
		throw new NotImplementedException();
	}
	public override async Task<Asset> FetchAssetByName(string name) {
		if(name.Contains("..") || name.Contains("./") || name.Contains(".\\")) throw new CommandException(1);
		var path = Path.Combine("Assets", name);
		if(!File.Exists(path)) throw new CommandException(1);
		var data = await File.ReadAllBytesAsync(path);
		var hash = SHA256.HashData(data); // Truncated to 128-bit by Uuid class
		return new Asset {
			Data = data, 
			Id = new Uuid(hash), 
			Name = name
		};
	}
	public override Task<Asset[]> FetchAssetsByIds(Uuid[] ids) {
		throw new NotImplementedException();
	}
	public override Task<Asset[]> FetchAssetsByNames(string[] names) {
		throw new NotImplementedException();
	}
	public override Task<Uuid> GetId(string name) {
		throw new NotImplementedException();
	}
}