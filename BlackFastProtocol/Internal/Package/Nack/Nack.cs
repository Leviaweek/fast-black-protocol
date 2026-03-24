using System.Buffers.Binary;
using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Package.Nack;

public sealed record Nack(int LostIds) : IPackageBody, IReadableData<Nack>
{
    private const byte IntSize = sizeof(int);

    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, IntSize), LostIds);

        return Length;
    }
    
    public static Nack ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < offset + IntSize)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;

        var lostIds = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, IntSize));

        return new Nack(lostIds);
    }

    public int Length => IntSize;
}