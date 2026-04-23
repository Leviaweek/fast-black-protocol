using System.Buffers.Binary;
using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Package.Ack;

/// <summary>
/// SACK-style acknowledgement: confirms all packets up to BaseSequence (inclusive)
/// plus any out-of-order packets indicated by ReceivedMask.
/// ReceiverWindow tells the sender how many more packets the receiver can buffer.
/// Wire format: uint BaseSequence (4) | uint ReceivedMask (4) | ushort ReceiverWindow (2) = 10 bytes.
/// </summary>
internal sealed record AckBody(
    uint BaseSequence,
    uint ReceivedMask,
    ushort ReceiverWindow
) : IPackageBody, IReadableData<AckBody>
{
    private const int Size = sizeof(uint) + sizeof(uint) + sizeof(ushort); // 10

    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Size + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));

        var span = buffer[offset..];
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), BaseSequence);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), ReceivedMask);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8, 2), ReceiverWindow);

        return Size;
    }

    public static AckBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Size + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));

        var span = buffer.Span[offset..];
        var baseSeq  = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
        var mask     = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
        var window   = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8, 2));

        return new AckBody(baseSeq, mask, window);
    }

    public int Length => Size;
}
