namespace BlackFastProtocol.Package;

public static class PackageRw
{
    public static T ReadPackage<T>(ReadOnlyMemory<byte> buffer) where T : IReadablePackage<T> => T.ReadPackage(buffer);
}