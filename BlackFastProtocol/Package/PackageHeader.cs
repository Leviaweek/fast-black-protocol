using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace BlackFastProtocol.Package;

public sealed unsafe record PackageHeader : ITypedPackage, IWriteableData, IReadableData<PackageHeader>, ILengthPackage
{
  public PackageHeader(Guid sessionId, PackageType type, uint id, DateTimeOffset timestamp)
  {
    SessionId = sessionId;
    _type = type;
    Sequence = id;
    Timestamp = timestamp;
  }

  private PackageType _type;
  public PackageType Type => _type;

  public int Length => sizeof(Guid) + sizeof(PackageType) + sizeof(uint);
  public Guid SessionId { get; }
  public uint Sequence { get; }
  public DateTimeOffset Timestamp { get; }

  public int WriteData(Span<byte> buffer, int offset = 0)
  {
    if (buffer.Length < Length + offset)
      throw new ArgumentException("Buffer too small", nameof(buffer));
        
    SessionId.TryWriteBytes(buffer[offset..]);
    buffer[offset + 16] = Unsafe.As<PackageType, byte>(ref _type);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 17, 4), Sequence);
    BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset + 21, 8), Timestamp.UtcTicks);
    BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(offset + 29, 2), (short)Timestamp.Offset.TotalMinutes);
    return Length;
  }

  public static PackageHeader ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
  {
    if (buffer.Length < 31 + offset)
      throw new ArgumentException("Buffer too small", nameof(buffer));
        
    var span = buffer.Span;
        
    var sessionId = new Guid(span.Slice(offset, 16));
        
    var type = (PackageType)span[offset + 16];
        
    var id = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 17, 4));
    
    var timestampTicks = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset + 21, 8));
    
    var utcTime = new DateTime(timestampTicks, DateTimeKind.Utc);
    
    var timestampOffsetMinutes = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset + 29, 2));
    
    var timestamp = new DateTimeOffset(utcTime).ToOffset(TimeSpan.FromMinutes(timestampOffsetMinutes));
        
    return new PackageHeader(sessionId, type, id, timestamp);
  }
  
  public static PackageHeader CreateFromContext(FastBlackSessionContext context, PackageType type)
  {
    var sessionId = context.SessionId;
    var sequence = context.GetNextSequence();
    var timestamp = DateTimeOffset.UtcNow;
    return new PackageHeader(sessionId, type, sequence, timestamp);
  }
}