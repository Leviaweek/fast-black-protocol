namespace BlackFastProtocol.Package.DataFrame;

public sealed record DataFrameBody(ReadOnlyMemory<byte> Data) : IPackageBody,
    IReadableData<DataFrameBody>
{
    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Data.Span.CopyTo(buffer.Slice(offset, Data.Length));
        return Length;
    }

    public static DataFrameBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < 21 + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var data = buffer[offset..].ToArray();
        
        return new DataFrameBody(data);
    }

    public int Length => Data.Length;
}