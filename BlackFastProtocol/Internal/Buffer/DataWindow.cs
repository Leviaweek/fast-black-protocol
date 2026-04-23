using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Buffer;

// Mutable buffer — class, not record (record equality is wrong with mutable fields).
internal sealed class DataWindow
{
    private readonly byte[] _buffer = new byte[BlackFastClient.MaxPayloadSize * BlackFastClient.WindowSize];
    private uint _expectedBytes;
    private int _bytesRead;
    private uint _filledMask;

    public DataWindow(uint startSequence, uint endSequence, uint expectedBytes)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
    }

    public uint StartSequence { get; private set; }
    public uint EndSequence { get; private set; }

    /// <summary>True when at least one packet has been written into this window.</summary>
    public bool HasData => _filledMask != 0;

    public bool Contains(uint sequence) => sequence >= StartSequence && sequence < EndSequence;

    public void Update(uint startSequence, uint endSequence, uint expectedBytes)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
        _filledMask = 0;
        _bytesRead = 0;
    }

    public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
    {
        if (!Contains(sequence)) return false;

        var diff = (int)(sequence - StartSequence);

        if (diff < 0 || diff >= BlackFastClient.WindowSize || data.Length > BlackFastClient.MaxPayloadSize)
            return false;

        var bit = 1u << diff;
        if ((_filledMask & bit) != 0) return false;

        data.CopyTo(_buffer.AsSpan(diff * BlackFastClient.MaxPayloadSize, data.Length));
        _bytesRead += data.Length;
        _filledMask |= bit;
        return true;
    }

    public byte[] Flush()
    {
        if (_bytesRead == 0 && _filledMask == 0) return [];

        // BUG-1 FIX: the old code did Array.Copy(_buffer, result, _bytesRead) which
        // always copies bytes starting from offset 0 of the flat internal array.
        // If slot 0 was never received (out-of-order delivery where early packets
        // arrived but the first one is still missing), the result would contain
        // zeroes from the unfilled slot-0 region instead of the actual data.
        //
        // Correct approach: walk the filled slots in ascending order and copy only
        // the bytes that were actually written. Each slot i owns exactly
        // [i*MaxPayloadSize .. i*MaxPayloadSize + slotBytes) in _buffer.
        // The last filled slot may hold fewer than MaxPayloadSize bytes.
        var result = new byte[_bytesRead];
        int destOffset = 0;
        int slotCount = (int)(EndSequence - StartSequence);

        for (int i = 0; i < slotCount; i++)
        {
            if ((_filledMask & (1u << i)) == 0) continue;

            int srcOffset = i * BlackFastClient.MaxPayloadSize;
            // All slots except possibly the last hold MaxPayloadSize bytes.
            // The last slot holds however many bytes remain in _bytesRead.
            int remaining = _bytesRead - destOffset;
            int slotBytes = Math.Min(BlackFastClient.MaxPayloadSize, remaining);

            Array.Copy(_buffer, srcOffset, result, destOffset, slotBytes);
            destOffset += slotBytes;
        }

        return result;
    }

    public bool IsReady() => _bytesRead == _expectedBytes;
}
