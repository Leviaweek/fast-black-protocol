namespace BlackFastProtocol.Package.Ack;

public sealed record AckPackageBody : IPackageBody, IReadableData<AckPackageBody>
{
    public int ToBytes(Span<byte> buffer, int offset = 0)
    {        
        return Length;
    }

    public static AckPackageBody ReadPackage(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        return new AckPackageBody();
    }

    public int Length => 0;
}