namespace BlackFastProtocol.Internal.Buffer;

internal sealed record DataWindow
{
    private readonly byte[] _buffer = new byte[999 * 32];
    private uint _expectedBytes;
    private int _bytesRead;

    public DataWindow(uint startSequence, uint endSequence, uint expectedBytes = 999 * 32)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
    }

    public uint StartSequence { get; private set; }
    public uint EndSequence { get; private set; }

    public bool Contains(uint sequence) => sequence >= StartSequence && sequence < EndSequence;

    public void Update(uint startSequence, uint endSequence, uint expectedBytes = 999 * 32)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
    }

    public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
    {
        if (!Contains(sequence))
        {
            return false;
        }

        var diff = (int)(sequence - StartSequence);
        
        if (diff < 0 || diff >= 32 || data.Length > 999)
        {
            return false;
        }
        
        data.CopyTo(_buffer.AsSpan(diff * 999, data.Length));
        _bytesRead += data.Length;
        return true;
    }

    public byte[] Flush()
    {
        var result = new byte[_bytesRead];
        Array.Copy(_buffer, result, _bytesRead);
        return result;
    }

    public bool IsReady() => _bytesRead == _expectedBytes;
}