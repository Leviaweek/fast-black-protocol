namespace BlackFastProtocol.Package;

public static class PackageRw
{
    public static T ReadPackage<T>(ReadOnlyMemory<byte> buffer) where T : PackageBase, ITypedPackage, IReadablePackage<T> => T.ReadPackage(buffer);
}