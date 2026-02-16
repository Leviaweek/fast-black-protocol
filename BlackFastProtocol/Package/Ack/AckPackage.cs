using System.Buffers.Binary;

namespace BlackFastProtocol.Package.Ack;

public sealed record AckPackage : PackageBase,
    IWriteablePackage, IReadablePackage<AckPackage>
{
    public AckPackage(Guid sessionId, int id) : base(sessionId, PackageType.Ack, id) { }
    private AckPackage(PackageHeader header) : base(header) { }

    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Header.ToBytes(buffer);
        
        return Length;
    }

    public static AckPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 21)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var header = PackageHeader.ReadPackage(buffer);
        
        return new AckPackage(header);
    }

    public override int Length => Header.Length;
}