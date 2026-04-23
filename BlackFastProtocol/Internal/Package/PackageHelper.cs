using System.Collections.Frozen;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Package.DataHeader;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Package.Ping;
using BlackFastProtocol.Internal.Package.Pong;

namespace BlackFastProtocol.Internal.Package;

internal static class PackageHelper
{
    public static FrozenDictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>> BodyReaders { get; } =
        new Dictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>>
        {
            [PackageType.Handshake]  = buf => HandshakeBody.ReadData(buf),
            [PackageType.Ack]        = buf => AckBody.ReadData(buf),
            [PackageType.DataHeader] = buf => DataHeaderBody.ReadData(buf),
            [PackageType.Data]       = buf => DataBody.ReadData(buf),
            [PackageType.Ping]       = buf => PingBody.ReadData(buf),
            [PackageType.Pong]       = buf => PongBody.ReadData(buf),
        }.ToFrozenDictionary();

    public static FrozenDictionary<PackageType, IBodyHandler> Handlers { get; } =
        new Dictionary<PackageType, IBodyHandler>
        {
            [PackageType.Handshake]  = new BodyHandlerAdapter<HandshakeBody>(new HandshakeBodyHandler()),
            [PackageType.Ack]        = new BodyHandlerAdapter<AckBody>(new AckBodyHandler()),
            [PackageType.DataHeader] = new BodyHandlerAdapter<DataHeaderBody>(new DataHeaderBodyHandler()),
            [PackageType.Data]       = new BodyHandlerAdapter<DataBody>(new DataBodyHandler()),
            [PackageType.Ping]       = new BodyHandlerAdapter<PingBody>(new PingBodyHandler()),
            [PackageType.Pong]       = new BodyHandlerAdapter<PongBody>(new PongBodyHandler()),
        }.ToFrozenDictionary();
}