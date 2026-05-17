using System.ComponentModel.DataAnnotations;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Package.DataPackage;

internal sealed record DataBody : IPackageBody, IReadableData<DataBody>
{
    public DataBody(ReadOnlyMemory<byte> data)
    {
        if (data.Length > BlackFastClient.MaxPayloadSize)
            throw new ArgumentOutOfRangeException(nameof(data),
                $"Payload exceeds {BlackFastClient.MaxPayloadSize} bytes.");

        Data = data;
    }

    [MaxLength(BlackFastClient.MaxPayloadSize)]
    public ReadOnlyMemory<byte> Data { get; }

    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Data.Span.CopyTo(buffer.Slice(offset, Data.Length));
        return Length;
    }

    public static DataBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < offset + 1)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var data = buffer[offset..].ToArray();
        return new DataBody(data);
    }

    public int Length => Data.Length;
}
