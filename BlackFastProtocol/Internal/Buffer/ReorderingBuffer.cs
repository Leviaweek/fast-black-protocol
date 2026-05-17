using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Buffer;

internal sealed class ReorderingBuffer(int length = 1024)
{
    private readonly ProtocolPackage?[] _buffer = new ProtocolPackage?[length];
    private readonly uint _mask = (uint)(length - 1);
    public int Length => length;

    public bool TryAdd(ProtocolPackage package)
    {
        var sequence = package.Header.Sequence;

        var index = sequence & _mask;

        if (_buffer[index] is not null)
        {
            return false;
        }

        _buffer[index] = package;
        return true;
    }

    public bool TryGetOrderedPackage(uint sequence, out ProtocolPackage? package)
    {
        var index = sequence & _mask;
        package = _buffer[index];
        if (package is null || package.Header.Sequence != sequence)
        {
            package = null;
            return false;
        }

        _buffer[index] = null;
        return true;
    }
    public uint GetPackagesMask(uint startSequence, uint endSequence)
    {
        var mask = 0u;
        
        var diff = (int)SequenceHelper.Distance(startSequence, endSequence);
        if (diff is > BlackFastClient.WindowSize or <= 0)
        {
            throw new ArgumentException($"End sequence must be less than start sequence + {BlackFastClient.WindowSize}");
        }
        
        for (var i = 0; i < diff; i++)
        {
            var sequence = startSequence + (uint)i;
            var index = sequence & _mask;
            if (_buffer[index]?.Header.Sequence == sequence)
            {
                mask |= 1u << i;
            }
        }

        return mask;
    }
}

internal sealed class OutgoingBuffer(int length = BlackFastClient.WindowSize)
{
    private readonly ProtocolPackage?[] _buffer = new ProtocolPackage?[length];
    
    private readonly uint _mask = (uint)(length - 1);
    public int Length => length;
    
    public bool TryAdd(ProtocolPackage package)
    {
        var sequence = package.Header.Sequence;

        var index = sequence & _mask;

        if (_buffer[index] is not null)
        {
            return false;
        }

        _buffer[index] = package;
        return true;
    }
    
    public void Set(ProtocolPackage package)
    {
        var sequence = package.Header.Sequence;

        var index = sequence & _mask;

        _buffer[index] = package;
    }
    
    public ProtocolPackage? Peek(uint sequence)
    {
        var index = sequence & _mask;
        return _buffer[index];
    }

    public void Clear(uint startSequence, uint endSequence)
    {
        for (var sequence = startSequence; sequence < endSequence; sequence++)
        {
            Remove(sequence);
        }
    }

    public void Clear() => Array.Clear(_buffer);
    
    public void Remove(uint sequence)
    {
        var index = sequence & _mask;
        _buffer[index] = null;
    }
}
