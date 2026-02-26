using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class FastBlackSessionContext(BlackFastClient client, Guid sessionId)
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
}