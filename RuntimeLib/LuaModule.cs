using System.Reflection;
using MoonSharp.Interpreter;

namespace RuntimeLib; 

public class LuaModule : IModule {
	public IReadOnlyDictionary<string, Delegate> Exports { get; }

	public LuaModule(string code, IReadOnlyDictionary<string, Delegate> ecExports) {
		try {
			var script = new Script();
			var globals = new Table(script);
			globals["debug"] = (Action<string>) (x => Console.WriteLine($"Debug message from Lua: {x}"));
			globals["tostring"] = (Func<int, string>) (x => x.ToString());
			//globals["add"] = (Func<int, int, int>) ((a, b) => a + b);
			foreach(var (name, del) in ecExports) {
				Console.WriteLine($"{name} -- {del}");
				
				globals[name] = DynValue.NewCallback(new DynamicMethodMemberDescriptor(del, del.GetMethodInfo(), InteropAccessMode.LazyOptimized).GetCallbackFunction(script, del.Target));
			}
			var func = script.LoadString(code, globals);
			Console.WriteLine($"Lua function? {func.Function}");
			func.Function.Call();
			Console.WriteLine("LOADED LUA MODULE!");
		} catch(Exception e) {
			Console.WriteLine(e);
		}
	}
}