using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Buffer;

internal sealed record DataWindow
{
    private readonly byte[] _buffer = new byte[BlackFastClient.MaxPayloadSize * BlackFastClient.WindowSize];
    private uint _expectedBytes;
    private int _bytesRead;

    public DataWindow(uint startSequence, uint endSequence, uint expectedBytes = 999 * BlackFastClient.WindowSize)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
    }

    public uint StartSequence { get; private set; }
    public uint EndSequence { get; private set; }

    public bool Contains(uint sequence) => sequence >= StartSequence && sequence < EndSequence;

    public void Update(uint startSequence, uint endSequence, uint expectedBytes = BlackFastClient.MaxPayloadSize * BlackFastClient.WindowSize)
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
        
        if (diff < 0 || diff >= BlackFastClient.WindowSize || data.Length > BlackFastClient.MaxPayloadSize)
        {
            return false;
        }
        
        data.CopyTo(_buffer.AsSpan(diff * BlackFastClient.MaxPayloadSize, data.Length));
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