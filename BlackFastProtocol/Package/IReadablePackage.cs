namespace BlackFastProtocol.Package;

public interface IReadablePackage<out T>
{
    public static abstract T ReadPackage(ReadOnlyMemory<byte> buffer);
}