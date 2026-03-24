using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.State;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Session;

internal sealed class FastBlackSessionContext(
    BlackFastClient client,
    Guid sessionId) : IDisposable
{
    public BlackFastClient Session { get; } = client;

    internal PackageTracker Tracker { get; } = new();
    internal SessionInfo Info { get; } = new(sessionId);
    internal SessionDataPipeline DataChannel { get; } = new();
    internal SequenceManager  SequenceManager { get; } = new();
    internal ReorderingBuffer ReorderingBuffer { get; } = new();
    internal TaskCompletionSource<IPackageBody>? AckAwaiter { get; set; }
    
    private volatile ClientState _clientState = new DefaultClientState();
    
    internal ClientState ClientState
    {
        get => _clientState;
        set => Interlocked.Exchange(ref _clientState, value);
    }
    
    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        Info.IsStarted = true;
        while (ReorderingBuffer.TryGetOrderedPackage(SequenceManager.Expected, out var package))
        {
            await HandleAllPackageAsync(package!, cancellationToken);
        }
    }
    

    public async Task HandlePackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var diff = (int)package.Header.Sequence - SequenceManager.Expected;
        if (diff < 0 || diff >= ReorderingBuffer.Length)
        {
            return;
        }

        if (!Info.IsStarted)
        {
            ReorderingBuffer.TryAdd(package);
            return;
        }
        
        if (package.Header.Sequence != SequenceManager.Expected)
        {
            ReorderingBuffer.TryAdd(package);
            
            return;
        }
        
        await HandleAllPackageAsync(package, cancellationToken);
    }

    private async Task HandleAllPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        await _clientState.HandleAsync(package, this, cancellationToken);

        if (Info.IsAborted)
        {
            Dispose();
            return;
        }
        
        while (ReorderingBuffer.TryGetOrderedPackage(SequenceManager.Expected, out var orderedPackage))
        {
            await _clientState.HandleAsync(orderedPackage!, this, cancellationToken);
            
            if (Info.IsAborted)
            {
                Dispose();
                return;
            }
        }
    }

    public void Dispose()
    {
        DataChannel.Dispose();
    }
}