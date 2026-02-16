using System.Buffers.Binary;

namespace BlackFastProtocol.Package;

public abstract record PackageBase: ILengthPackage
{
    protected PackageBase(PackageHeader header)
    {
        Header = header;
    }
    
    protected PackageBase(Guid sessionId, PackageType type, int id)
    {
        Header = new PackageHeader(sessionId, type, id); ;
    }

    public PackageHeader Header { get; }
    public abstract int Length { get; }
}


public sealed unsafe record PackageHeader(Guid SessionId, PackageType Type, int Id)
    : ITypedPackage, IWriteablePackage, IReadablePackage<PackageHeader>, ILengthPackage
{
    
    public int Length => sizeof(Guid) + sizeof(PackageType) + sizeof(int);
    
    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < 21)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        SessionId.TryWriteBytes(buffer);
        buffer[16] = (byte)Type;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(17, 4), Id);
        return 21;
    }

    public static PackageHeader ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 21)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var sessionId = new Guid(span[..16]);
        
        var type = (PackageType)span[16];
        
        var id = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(17, 4));
        
        return new PackageHeader(sessionId, type, id);
    }
}

public interface ILengthPackage
{
    public int Length { get; }
}