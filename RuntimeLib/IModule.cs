namespace RuntimeLib; 

public interface IModule {
	IReadOnlyDictionary<string, Delegate> Exports { get; }
}