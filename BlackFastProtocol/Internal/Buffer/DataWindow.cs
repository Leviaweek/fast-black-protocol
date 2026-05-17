using BlackFastProtocol.Public;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Buffer;

// Mutable buffer — class, not record (record equality is wrong with mutable fields).
internal sealed class DataWindow
{
    private readonly byte[] _buffer = new byte[BlackFastClient.MaxPayloadSize * BlackFastClient.WindowSize];
    private uint _expectedBytes;
    private int _bytesRead;
    private uint _filledMask;
    private readonly int[] _slotLengths = new int[BlackFastClient.WindowSize];
    private int _slotCount;

    public DataWindow(uint startSequence, uint endSequence, uint expectedBytes)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
        _slotCount = (int)SequenceHelper.Distance(startSequence, endSequence);
    }

    public uint StartSequence { get; private set; }
    public uint EndSequence { get; private set; }

    /// <summary>True when at least one packet has been written into this window.</summary>
    public bool HasData => _filledMask != 0;

    public bool Contains(uint sequence) => SequenceHelper.Distance(StartSequence, sequence) < (uint)_slotCount;

    public void Update(uint startSequence, uint endSequence, uint expectedBytes)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        _expectedBytes = expectedBytes;
        _filledMask = 0;
        _bytesRead = 0;
        _slotCount = (int)SequenceHelper.Distance(startSequence, endSequence);
        Array.Clear(_slotLengths);
    }

    public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
    {
        if (!Contains(sequence)) return false;
        
        var diff = (int)SequenceHelper.Distance(StartSequence, sequence);

        if (diff < 0 || diff >= BlackFastClient.WindowSize
                     || data.Length == 0
                     || data.Length > BlackFastClient.MaxPayloadSize)
            return false;

        var bit = 1u << diff;
        if ((_filledMask & bit) != 0) return false;

        data.CopyTo(_buffer.AsSpan(diff * BlackFastClient.MaxPayloadSize, data.Length));
        _bytesRead += data.Length;
        _slotLengths[diff] = data.Length;
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
        var destOffset = 0;
        for (var i = 0; i < _slotCount; i++)
        {
            if ((_filledMask & (1u << i)) == 0) continue;

            var srcOffset = i * BlackFastClient.MaxPayloadSize;
            var slotBytes = _slotLengths[i];

            Array.Copy(_buffer, srcOffset, result, destOffset, slotBytes);
            destOffset += slotBytes;
        }

        return result;
    }

    public (uint BaseSequence, uint ReceivedMask) CreateAck()
    {
        var prefixLength = 0;
        while (prefixLength < _slotCount && (_filledMask & (1u << prefixLength)) != 0)
            prefixLength++;

        var baseSequence = StartSequence + (uint)prefixLength - 1;
        var mask = 0u;

        for (var i = prefixLength; i < _slotCount; i++)
        {
            if ((_filledMask & (1u << i)) == 0) continue;

            var dist = i - prefixLength;
            if (dist < 32)
                mask |= 1u << dist;
        }

        return (baseSequence, mask);
    }

    public bool IsReady()
    {
        var expectedMask = _slotCount == 32 ? uint.MaxValue : (1u << _slotCount) - 1;
        return _bytesRead == _expectedBytes && (_filledMask & expectedMask) == expectedMask;
    }
}
