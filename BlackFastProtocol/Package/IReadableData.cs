namespace BlackFastProtocol.Package;

public interface IReadableData<out T>
{
    public static abstract T ReadPackage(ReadOnlyMemory<byte> buffer, int offset = 0);
}