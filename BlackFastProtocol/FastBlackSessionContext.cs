using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class FastBlackSessionContext(BlackFastClient client, Guid sessionId)
{
  public BlackFastClient Session { get; } = client;
  public bool IsAborted { get; set; }

  public bool IsHandshake { get; set; }
  public IPackageBody? LastReceivedPackage { get; set; }
  public ProtocolPackage? LastSentPackage { get; set; }
    
  private int _currentSequence = int.MinValue;
  public int CurrentSequence { 
    get => _currentSequence; 
    set => Interlocked.Exchange(ref _currentSequence, value); 
  }
    
  public int GetNextSequence() => Interlocked.Increment(ref _currentSequence);
  public Guid SessionId { get; } = sessionId;
}