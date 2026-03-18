using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Session;

internal sealed class PackageTracker
{
    public IPackageBody? LastReceivedPackage { get; set; }
    public ProtocolPackage? LastSentPackage { get; set; }
}