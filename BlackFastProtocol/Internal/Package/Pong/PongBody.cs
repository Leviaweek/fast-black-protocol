using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Package.Pong;

internal sealed record PongBody: IPackageBody, IReadableData<PongBody>
{
    public int Length => 0;
    public int WriteData(Span<byte> buffer, int offset = 0) => Length;

    public static PongBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0) => new();
}