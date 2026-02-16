using System.Buffers.Binary;

namespace BlackFastProtocol.Package.Nack;

public sealed record NackPackage : PackageBase,
        IWriteablePackage, IReadablePackage<NackPackage>
{
    public NackPackage(Guid sessionId, int id, ReadOnlyMemory<int> lostIds) : base(sessionId, PackageType.UnAck, id)
    {
        LostIds = lostIds;
    }
    private NackPackage(PackageHeader header, ReadOnlyMemory<int> lostIds) : base(header)
    {
        LostIds = lostIds;
    }

    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Header.ToBytes(buffer);
        
        for (var i = 0; i < LostIds.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(Header.Length + i * 4, 4), LostIds.Span[i]);
        }

        return Length;
    }
    
    public static NackPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var header = PackageHeader.ReadPackage(buffer);
        
        var length = (buffer.Length - header.Length) / sizeof(int);
        var lostIds = new int[length];
        for (var i = 0; i < length; i++)
        {
            lostIds[i] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(header.Length + i * 4, 4));
        }
        return new NackPackage(header, lostIds);
    }
    
    public ReadOnlyMemory<int> LostIds { get; }

    public override int Length => Header.Length + LostIds.Length * sizeof(int);
}