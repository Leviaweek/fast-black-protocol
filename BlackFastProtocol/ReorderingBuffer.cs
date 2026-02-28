using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class ReorderingBuffer(int length = 1024)
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
}