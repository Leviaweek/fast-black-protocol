using System.Threading.Channels;
using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class FastBlackSessionContext(BlackFastClient client, Guid sessionId, ChannelWriter<byte[]> dataChannelWriter)
{
  public BlackFastClient Session { get; } = client;
  public bool IsAborted { get; set; }

  public bool IsHandshake { get; set; }
  public IPackageBody? LastReceivedPackage { get; set; }
  public ProtocolPackage? LastSentPackage { get; set; }
    
  private uint _currentSequence = uint.MaxValue;
  public uint CurrentSequence { 
    get => _currentSequence; 
    set => Interlocked.Exchange(ref _currentSequence, value); 
  }
    
  public uint GetNextSequence() => Interlocked.Increment(ref _currentSequence);
  public Guid SessionId { get; } = sessionId;
  public ChannelWriter<byte[]> DataChannelWriter { get; } = dataChannelWriter;
  public DataAccumulator? DataAccumulator { get; set; }
}

// 33 - 0
// 44 - 11
// _buffer[0..length] = data
// _buffer[999..length] = data

//getData -> _buffer[0..999 * 11].ToArray();

public sealed record DataAccumulator(int Length)
{
  public DataWindow Window = new(0, 32);
}
public sealed record DataWindow(uint StartSequence, uint EndSequence)
{
  private readonly byte[] _buffer = new byte[999 * 32];
  private uint _currentSequence = StartSequence;
  private int _readOffset;
  
  public bool Contains(uint sequence) => sequence >= StartSequence && sequence < EndSequence;
  public void Clear() => Array.Clear(_buffer);
  public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
  {
    if (!Contains(sequence))
    {
      return false;
    }
    
    data.CopyTo(_buffer.AsSpan(_readOffset, data.Length));
    _readOffset += data.Length;
    return true;
  }

  public byte[] Flush()
  {
    var result = new byte[_readOffset];
    Array.Copy(_buffer, result, _readOffset);
    Clear();
    return result;
  }
}