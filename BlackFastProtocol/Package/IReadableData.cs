namespace BlackFastProtocol.Package;

public interface IReadableData<out T>
{
    public static abstract T ReadData(ReadOnlyMemory<byte> buffer, int offset = 0);
}