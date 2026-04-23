using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Package.Ping;

internal sealed record PingBody: IPackageBody, IReadableData<PingBody>
{
    public int Length => 0;
    public int WriteData(Span<byte> buffer, int offset = 0) => Length;

    public static PingBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0) => new();
}