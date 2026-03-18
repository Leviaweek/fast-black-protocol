using BlackFastProtocol.Package;

namespace BlackFastProtocol;

internal sealed class PackageTracker
{
    public IPackageBody? LastReceivedPackage { get; set; }
    public ProtocolPackage? LastSentPackage { get; set; }
}