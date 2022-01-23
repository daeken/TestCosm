namespace RuntimeLib; 

public class RuntimeExecutionContext : IDisposable {
	public readonly List<IModule> Modules = new();
	public readonly Dictionary<string, Delegate> Exports = new();

	public int LoadWasmModule(byte[] data, Dictionary<string, string> exports) {
		var module = new WasmModule(data, exports);
		return AddModule(module);
	}

	public int LoadLuaScript(string code) {
		var module = new LuaModule(code, Exports);
		return AddModule(module);
	}

	int AddModule(IModule module) {
		lock(Modules) {
			var count = Modules.Count;
			Modules.Add(module);
			if(module.Exports != null)
				foreach(var (name, del) in module.Exports)
					Exports[name] = del;
			return count;
		}
	}
	
	public void Dispose() {}
}