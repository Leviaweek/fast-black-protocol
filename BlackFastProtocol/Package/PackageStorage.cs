namespace BlackFastProtocol.Package;

public sealed class PackageStorage
{
    public PackageBase? LastReceivedPackage { get; set; }
    public IWriteablePackage? LastSentPackage { get; set; }
}