using BlackFastProtocol.Internal.Package;

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
        if (package is null)
        {
            return false;
        }

        _buffer[index] = null;
        return true;
    }
    public uint GetPackagesMask(uint startSequence, uint endSequence)
    {
        var mask = 0u;
        
        if (startSequence >= endSequence)
        {
            throw new ArgumentException("Start sequence must be less than end sequence");
        }
        
        var diff = (int)(endSequence - startSequence);
        if (diff > 32)
        {
            throw new ArgumentException("End sequence must be less than start sequence + 32");
        }
        
        for (var sequence = startSequence; sequence < endSequence; sequence++)
        {
            var index = sequence & _mask;
            if (_buffer[index] is not null)
            {
                mask |= 1u << (int)(sequence - startSequence);
            }
        }

        return mask;
    }
}