using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Buffer;

internal sealed record DataAccumulator: IDisposable
{
    private int _totalReceivedBytes;
    public readonly DataWindow Window = new(0, BlackFastClient.WindowSize);
    private DateTimeOffset _lastUpdateTime = DateTimeOffset.MinValue;
    private readonly CancellationTokenSource _src;

    public DataAccumulator(int length)
    {
        Length = length;
        _src = new CancellationTokenSource();
        _ = TimeoutFlushAsync(_src.Token);
    }

    public int Length { get; init; }

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

    public byte[] FlushWindow()
    {
        var data = Window.Flush();
        _totalReceivedBytes += data.Length;
        UpdateWindow();
        return data;
    }
    
    public bool TryAdd(uint sequence, ReadOnlySpan<byte> data)
    {
        var result = Window.TryAdd(sequence, data);
        
        if (result)
        {
            _lastUpdateTime = DateTimeOffset.UtcNow;
        }
        
        return result;
    }
    
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromMilliseconds(200);

    private async Task TimeoutFlushAsync(CancellationToken cancellationToken)
    {
        var delayTime = WindowTimeout / 4;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow - _lastUpdateTime > WindowTimeout)
            {
                FlushWindow();
            }

            await Task.Delay(delayTime / 4, cancellationToken);
        }
    }

    public void Dispose() => _src.Dispose();
}