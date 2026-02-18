using System.Buffers.Binary;

namespace BlackFastProtocol.Package.Nack;

public sealed record NackPackage(ReadOnlyMemory<int> LostIds) : IPackageBody, IReadableData<NackPackage>
{
    private const byte IntSize = sizeof(int);

    public int ToBytes(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        for (var i = 0; i < LostIds.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset + i * IntSize, IntSize), LostIds.Span[i]);
        }

        return Length;
    }
    
    public static NackPackage ReadPackage(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < offset + IntSize)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;

        var length = (buffer.Length - offset) / sizeof(int);
        var lostIds = new int[length];

        for (var i = 0; i < length; i++)
        {
            lostIds[i] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset + i * IntSize, IntSize));
        }

        return new NackPackage(lostIds);
    }

    public int Length => LostIds.Length * IntSize;
}