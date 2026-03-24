using System.Collections.Frozen;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Package;

internal static class PackageHelper
{
    public static FrozenDictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>> BodyReaders { get; } =
        new Dictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>>
        {
            [PackageType.Handshake] = buffer => HandshakeBody.ReadData(buffer),
            [PackageType.Data] = buffer => DataBody.ReadData(buffer),
        }.ToFrozenDictionary();
    
    public static FrozenDictionary<PackageType, IBodyHandler> Handlers { get; } = new Dictionary<PackageType, IBodyHandler> {
        [PackageType.Handshake] = new BodyHandlerAdapter<HandshakeBody>(new HandshakeBodyHandler()),
        [PackageType.Data] = new BodyHandlerAdapter<DataBody>(new DataBodyHandler()),
    }.ToFrozenDictionary();
}