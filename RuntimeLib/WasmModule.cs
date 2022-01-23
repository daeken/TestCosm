using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using WebAssembly.Runtime;

namespace RuntimeLib; 

public class WasmModule : IModule {
	public IReadOnlyDictionary<string, Delegate> Exports { get; }
	public WasmModule(byte[] data, Dictionary<string, string> exports) {
		try {
			using var ms = new MemoryStream(data);
			var ic = Compile.FromBinary<object>(ms);
			var imports = new ImportDictionary();
			UnmanagedMemory mem = null;
			Func<int, string> GetString = offset => Marshal.PtrToStringUTF8(mem.Start + offset);
			int LogHit(string name, int ret = 1) {
				Console.WriteLine($"Hit {name}");
				return ret;
			}
			imports.Add("env", "debug", new FunctionImport((int foo) => Console.WriteLine($"Debug message from WASM! {foo} -- '{GetString(foo)}'")));
			imports.Add("wasi_snapshot_preview1", "fd_seek", new FunctionImport((int _, long _, int _, int _) => LogHit("fd_seek")));
			imports.Add("wasi_snapshot_preview1", "fd_write", new FunctionImport((int _, int _, int _, int _) => LogHit("fd_write")));
			imports.Add("wasi_snapshot_preview1", "fd_close", new FunctionImport((int _) => LogHit("fd_close")));
			imports.Add("wasi_snapshot_preview1", "fd_fdstat_get", new FunctionImport((int _, int _) => LogHit("fd_fdstat_get")));
			imports.Add("wasi_snapshot_preview1", "fd_prestat_get", new FunctionImport((int _, int _) => LogHit("fd_prestat_get", 8)));
			imports.Add("wasi_snapshot_preview1", "fd_prestat_dir_name", new FunctionImport((int _, int _, int _) => LogHit("fd_prestat_dir_name")));
			imports.Add("wasi_snapshot_preview1", "proc_exit", new FunctionImport((int _) => Console.WriteLine("WASM module attempted to exit!")));
			imports.Add("wasi_snapshot_preview1", "path_open", new FunctionImport((int _, int _, int _, int _, int _, long _, long _, int _, int _) => LogHit("path_open")));
			imports.Add("wasi_snapshot_preview1", "fd_fdstat_set_flags", new FunctionImport((int _, int _) => LogHit("fd_fdstat_set_flags")));
			imports.Add("wasi_snapshot_preview1", "fd_read", new FunctionImport((int _, int _, int _, int _) => LogHit("fd_read")));
			
			var instance = ic(imports);
			mem = ((dynamic) instance.Exports).memory;
			var et = instance.Exports.GetType();
			var methods = et.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
				.Where(x => et.GetProperties().All(y => y.GetMethod != x)).ToDictionary(x => x.Name);

			var ed = new Dictionary<string, Delegate>();
			foreach(var (name, signature) in exports) {
				if(!methods.TryGetValue(name, out var mi)) continue;
				if(!ParseSignature(signature, out var args, out var ret)) continue;
				Console.WriteLine($"Parsed signature. Got {args.Count} args (vs {mi.GetParameters().Length}) and ret {ret} (vs {mi.ReturnType})");
				if(args.Count != mi.GetParameters().Length) continue;

				var pexprs = args.Select((x, i) => Expression.Parameter(x, $"_{i}")).ToList();

				var argExprs = args.Zip(mi.GetParameters()).Select((x, i) => {
					var (st, pi) = x;
					var pt = pi.ParameterType;
					var argExpr = pexprs[i];
					if(st == pt) return argExpr;
					return Type.GetTypeCode(st) switch {
						TypeCode.String => argExpr, 
						_ => throw new NotImplementedException($"Unhandled argument type for WASM export: {st} {pt}")
					};
				});
				var callExpr = Expression.Call(Expression.Constant(instance.Exports), mi, argExprs);
				if(ret != mi.ReturnType)
					callExpr = Type.GetTypeCode(ret) switch {
						TypeCode.String => Expression.Call(Expression.Constant(GetString.Target), GetString.Method, callExpr),
						_ => throw new NotImplementedException($"Unhandled return type for WASM export: {ret} {mi.ReturnType}")
					};
				var funcExpr = Expression.Lambda(callExpr, pexprs);
				ed[name] = funcExpr.Compile();
			}
			Exports = ed;
		} catch(Exception e) {
			Console.WriteLine(e);
		}
	}

	static bool ParseSignature(string sig, out List<Type> args, out Type ret) {
		args = null;
		ret = null;
		
		sig = sig.Trim();
		if(sig.Length == 0) {
			args = new();
			ret = typeof(void);
			return true;
		}
		Type MapType(string type) {
			switch(type) {
				case "i32": return typeof(int);
				case "string": return typeof(string);
				default: throw new NotImplementedException($"Unhandled type in WASM export signature: '{type}'");
			}
		}
		
		Console.WriteLine($"Parsing signature: {sig}");

		if(sig[0] == '(') {
			if(!sig.Contains(')')) return false;
			var split = sig.Split(')');
			var argstr = split[0][1..].Trim();
			args = argstr.Length == 0 ? new() : argstr.Split(',').Select(x => MapType(x.Trim())).ToList();
			split = split[1].Split("->");
			ret = split.Length == 1 ? typeof(void) : MapType(split[1].Trim());
		} else {
			var split = sig.Split("->");
			args = new() { MapType(split[0].Trim()) };
			ret = split.Length == 1 ? typeof(void) : MapType(split[1].Trim());
		}
		return true;
	}
}