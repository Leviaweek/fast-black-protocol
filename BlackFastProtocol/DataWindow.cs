namespace BlackFastProtocol;

public sealed record DataWindow
{
    private readonly byte[] _buffer = new byte[999 * 32];
    private uint _currentSequence;
    private int _readOffset;
    private uint _expectedBytes = 999 * 32;

    public DataWindow(uint startSequence, uint endSequence)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _currentSequence = StartSequence;
    }

    public uint StartSequence { get; private set; }
    public uint EndSequence { get; private set; }

    public bool Contains(uint sequence) => sequence >= StartSequence && sequence < EndSequence;

    public void Update(uint startSequence, uint endSequence, uint expectedBytes = 999 * 32)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _currentSequence = startSequence;
        _readOffset = 0;
        _expectedBytes = expectedBytes;
    }

    public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
    {
        if (!Contains(sequence))
        {
            return false;
        }

        data.CopyTo(_buffer.AsSpan(_readOffset, data.Length));
        _readOffset += data.Length;
        _currentSequence = sequence;
        return true;
    }

    public byte[] Flush()
    {
        var result = new byte[_readOffset];
        Array.Copy(_buffer, result, _readOffset);
        return result;
    }

    public bool IsReady()
    {
        return _readOffset >= _expectedBytes || (_readOffset > 0 && _currentSequence == EndSequence - 1);
    }
}