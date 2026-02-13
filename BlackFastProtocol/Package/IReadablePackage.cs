namespace UdpServer.Packages;

public interface IReadablePackage<out T> where T : Package, ITypedPackage
{
    public static abstract T ReadPackage(ReadOnlyMemory<byte> buffer);
}