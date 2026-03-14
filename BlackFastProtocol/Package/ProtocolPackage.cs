namespace BlackFastProtocol.Package;

public sealed class ProtocolPackage: ILengthPackage
{
    public ProtocolPackage(PackageHeader header, IPackageBody body)
    {
        Header = header;
        Body = body;
    }

    public PackageHeader Header { get; }
    public IPackageBody Body { get; }
    public int Length => Header.Length + Body.Length;
}