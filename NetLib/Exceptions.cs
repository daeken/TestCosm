namespace NetLib; 

public class UnknownCommandException : Exception {
}

public class UnknownObjectException : Exception {
}

public class SerializationException : Exception {
}

public class DisconnectedException : Exception {
}

public class CommandException : Exception {
	public readonly int Error;
	public CommandException(int error) => Error = error;
}

