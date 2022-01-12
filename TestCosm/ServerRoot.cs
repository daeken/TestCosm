using NetLib;
using NetLib.Generated;
using Object = NetLib.Generated.Object;

namespace TestCosm; 

public class ServerRoot : BaseRoot {
	public ServerRoot(IConnection connection) : base(connection) {}
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