using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class ReorderingBuffer(int size = 1024)
{
  private readonly ProtocolPackage?[] _buffer = new ProtocolPackage?[size];
  private readonly uint _mask = (uint)(size - 1);
  private uint _startSequence = uint.MinValue;

  public bool TryAdd(ProtocolPackage package)
  {
    var sequence = package.Header.Sequence;

    var diff = (int)(sequence - _startSequence);
    
    if (diff < 0) return false;

    if (diff >= _buffer.Length) return false;

    var index = sequence & _mask;

    if (_buffer[index] is not null)
    {
      return false;
    }

    _buffer[index] = package;
    return true;
  }

  public IEnumerable<ProtocolPackage> GetOrderedPackages()
  {
    while (true)
    {
      var index = _startSequence & _mask;
      var package = _buffer[index];
      if (package is null)
      {
        yield break;
      }

      _buffer[index] = null;
      _startSequence++;
      yield return package;
    }
  }
}