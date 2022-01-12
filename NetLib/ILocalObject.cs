namespace NetLib; 

public interface ILocalObject {
	Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset);
}