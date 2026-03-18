using System.Collections.Frozen;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;
using BlackFastProtocol.Package.Handshake;

namespace BlackFastProtocol;

internal static class PackageHelper
{
    public static FrozenDictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>> BodyReaders { get; } =
        new Dictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>>
        {
            [PackageType.Handshake] = buffer => HandshakeBody.ReadData(buffer),
            [PackageType.DataPackage] = buffer => DataPackageBody.ReadData(buffer),
        }.ToFrozenDictionary();
    
    public static FrozenDictionary<PackageType, IBodyHandler> Handlers { get; } = new Dictionary<PackageType, IBodyHandler> {
        [PackageType.Handshake] = new BodyHandlerAdapter<HandshakeBody>(new HandshakeBodyHandler()),
        [PackageType.DataPackage] = new BodyHandlerAdapter<DataPackageBody>(new DataPackageBodyHandler()),
    }.ToFrozenDictionary();
}