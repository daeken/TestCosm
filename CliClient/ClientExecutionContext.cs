using System.Text;
using NetLib;
using NetLib.Generated;
using RuntimeLib;

namespace CliClient; 

public class ClientExecutionContext : BaseExecutionContext {
	readonly RuntimeExecutionContext ExecutionContext = new();
	
	public ClientExecutionContext(IConnection connection) : base(connection) {}
	public override async Task<ulong> LoadWasmModule(Uuid assetId, Dictionary<string, string> exports) =>
		(ulong) ExecutionContext.LoadWasmModule((await Program.AssetDelivery.FetchById(assetId)).Data, exports);
	public override async Task<ulong> LoadLuaScript(Uuid assetId) =>
		(ulong) ExecutionContext.LoadLuaScript(Encoding.UTF8.GetString((await Program.AssetDelivery.FetchById(assetId)).Data));
	public override Task<ulong> LoadInlineLuaScript(string script) {
		throw new NotImplementedException();
	}
	public override Task BeginExecution(ulong moduleOrScript, string entryPoint) {
		throw new NotImplementedException();
	}
	public override Task BeginInlineLuaExecution(string script) {
		throw new NotImplementedException();
	}
}