using System.ComponentModel.DataAnnotations;

namespace BlackFastProtocol.Package.DataPackage;

public sealed record DataPackageBody([MaxLength(1024)] ReadOnlyMemory<byte> Data) : IPackageBody, IReadableData<DataPackageBody>
{
    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Data.Span.CopyTo(buffer.Slice(offset, Data.Length));
        return Length;
    }

    public static DataPackageBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < offset + 1)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var data = buffer[offset..].ToArray();
        return new DataPackageBody(data);
    }

    public int Length => Data.Length;
}