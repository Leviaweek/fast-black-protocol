using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace BlackFastProtocol.Package;

public sealed unsafe record PackageHeader : ITypedPackage, IWriteableData, IReadableData<PackageHeader>, ILengthPackage
{
  public PackageHeader(Guid sessionId, PackageType type, uint id)
  {
    SessionId = sessionId;
    _type = type;
    Sequence = id;
  }

  private PackageType _type;
  public PackageType Type => _type;

  public int Length => sizeof(Guid) + sizeof(PackageType) + sizeof(uint);
  public Guid SessionId { get; }
  public uint Sequence { get; }

  public int WriteData(Span<byte> buffer, int offset = 0)
  {
    if (buffer.Length < Length + offset)
      throw new ArgumentException("Buffer too small", nameof(buffer));
        
    SessionId.TryWriteBytes(buffer.Slice(offset));
    buffer[offset + 16] = Unsafe.As<PackageType, byte>(ref _type);
    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 17, 4), Sequence);
    return Length;
  }

  public static PackageHeader ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
  {
    if (buffer.Length < 21 + offset)
      throw new ArgumentException("Buffer too small", nameof(buffer));
        
    var span = buffer.Span;
        
    var sessionId = new Guid(span.Slice(offset, 16));
        
    var type = (PackageType)span[offset + 16];
        
    var id = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 17, 4));
        
    return new PackageHeader(sessionId, type, id);
  }
}