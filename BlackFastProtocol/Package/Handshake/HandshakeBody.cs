namespace BlackFastProtocol.Package.Handshake;

public sealed record HandshakeBody : IPackageBody,
        IReadableData<HandshakeBody>
{
    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        return Length;
    }

    public static HandshakeBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        return new HandshakeBody();
    }
    
    public int Length => 0;
}