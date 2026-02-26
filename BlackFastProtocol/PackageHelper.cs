using System.Collections.Frozen;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.Handshake;

namespace BlackFastProtocol;

public static class PackageHelper
{
    public static FrozenDictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>> BodyReaders { get; } =
        new Dictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>>
        {
            [PackageType.Handshake] = buffer => HandshakeBody.ReadData(buffer),
        }.ToFrozenDictionary();
    
    public static FrozenDictionary<PackageType, IBodyHandler> Handlers { get; } = new Dictionary<PackageType, IBodyHandler> {
        [PackageType.Handshake] = new BodyHandlerAdapter<HandshakeBody>(new HandshakeBodyHandler()),
    }.ToFrozenDictionary();
}