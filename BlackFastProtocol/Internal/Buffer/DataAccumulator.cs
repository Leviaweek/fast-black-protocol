using System.Threading.Channels;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Buffer;

// Mutable buffer state — class, not record.
internal sealed class DataAccumulator : IDisposable
{
    private readonly ChannelWriter<byte[]> _dataChannel;
    private int _totalReceivedBytes;
    public readonly DataWindow Window;

    // BUG-5 FIX: the original constructor always passed startSequence=0 to DataWindow,
    // which is only correct for the very first transfer whose first data packet has
    // sequence 0. For any subsequent transfer (or any transfer that starts at a
    // non-zero sequence because prior Handshake/DataHeader packets already advanced
    // SequenceManager.Expected), the window start must be the CURRENT expected
    // incoming sequence — otherwise TryAdd rejects every real packet because
    // Contains() returns false (packet.Sequence >= 0 but < WindowSize is fine for
    // the first window; however after a Reload the start can be e.g. 33, 65, …).
    //
    // The caller (DataHeaderBodyHandler) now passes the current expected sequence.
    public DataAccumulator(int length, ChannelWriter<byte[]> dataChannel, uint startSequence)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Data length must be positive.");

        _dataChannel = dataChannel;
        Length = length;

        int windowBytes = (int)Math.Min((long)length,
            (long)BlackFastClient.MaxPayloadSize * BlackFastClient.WindowSize);

        int packagesInWindow = (windowBytes + BlackFastClient.MaxPayloadSize - 1)
                               / BlackFastClient.MaxPayloadSize;

        Window = new DataWindow(
            startSequence,
            startSequence + (uint)packagesInWindow,
            (uint)windowBytes);

    }

    public int Length { get; private set; }

    public void UpdateWindow() => UpdateWindow(Window.EndSequence);

    public bool IsComplete() => _totalReceivedBytes >= Length;

    public void UpdateWindow(uint startSequence)
    {
        var remainingBytes = Length - _totalReceivedBytes;
        if (remainingBytes <= 0) return;

        const int maxWindowSize = BlackFastClient.MaxPayloadSize * BlackFastClient.WindowSize;
        var bytesInWindow = Math.Min(remainingBytes, maxWindowSize);
        var packagesInWindow = (bytesInWindow + BlackFastClient.MaxPayloadSize - 1) / BlackFastClient.MaxPayloadSize;

        Window.Update(startSequence, startSequence + (uint)packagesInWindow, (uint)bytesInWindow);
    }

    public void Reload(uint startSequence, uint expectedBytes)
    {
        _totalReceivedBytes = 0;
        Length = (int)expectedBytes;
        UpdateWindow(startSequence);
    }

    public void FlushWindow()
    {
        var data = Window.Flush();

        if (data.Length == 0) return;

        _dataChannel.TryWrite(data);
        _totalReceivedBytes += data.Length;
    }

    public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
        => Window.TryAdd(sequence, data);

    public void Dispose() { }
}
