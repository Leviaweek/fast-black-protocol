namespace UdpServer.Packages;

public static class PackageRw
{
    public static T ReadPackage<T>(ReadOnlyMemory<byte> buffer) where T : Package, ITypedPackage, IReadablePackage<T>
    {
        return T.ReadPackage(buffer);
    }
}