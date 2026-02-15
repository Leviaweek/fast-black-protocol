namespace BlackFastProtocol.Package;

public interface IReadablePackage<out T> where T : PackageBase, ITypedPackage
{
    public static abstract T ReadPackage(ReadOnlyMemory<byte> buffer);
}