namespace UdpServer.Packages;

public sealed class PackageStorage
{
    public Package? LastReceivedPackage { get; set; }
    public IWriteablePackage? LastSentPackage { get; set; }
}