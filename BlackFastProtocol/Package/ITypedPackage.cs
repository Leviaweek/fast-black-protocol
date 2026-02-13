namespace UdpServer.Packages;

public interface ITypedPackage
{
    public PackageType Type { get; }
}