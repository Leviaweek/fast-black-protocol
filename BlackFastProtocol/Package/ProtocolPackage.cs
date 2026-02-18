namespace BlackFastProtocol.Package;

public sealed record ProtocolPackage: ILengthPackage
{
    internal ProtocolPackage(PackageHeader header, IPackageBody body)
    {
        Header = header;
        Body = body;
    }
    
    internal ProtocolPackage(Guid sessionId, PackageType type, int id, IPackageBody body)
    {
        Header = new PackageHeader(sessionId, type, id);
        Body = body;
    }

    public PackageHeader Header { get; }
    public IPackageBody Body { get; }
    public int Length => Header.Length + Body.Length;
}