using System.Threading.Channels;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.Ack;

namespace BlackFastProtocol;

public sealed class FastBlackSessionContext(
    BlackFastClient client,
    Guid sessionId) : IDisposable
{
    public BlackFastClient Session { get; } = client;

    internal PackageTracker Tracker { get; } = new();
    internal SessionInfo Info { get; } = new(sessionId);
    internal SessionDataPipeline DataChannel { get; } = new();
    internal SequenceManager  SequenceManager { get; } = new();
    internal ReorderingBuffer ReorderingBuffer { get; } = new();
    
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

internal abstract class ClientState
{
    public abstract Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}

internal sealed class DefaultClientState : ClientState
{
    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.SequenceManager.AdvanceExpected();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);
    }
}

internal sealed class StreamClientState(int dataLength = 0) : ClientState, IDisposable
{
    internal DataAccumulator? DataAccumulator { get; private set; } = dataLength > 0 ? new DataAccumulator(dataLength) : null;

    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.SequenceManager.AdvanceExpected();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);

        await PostProcessDataAsync(package, context, cancellationToken);
    }
    
    private async Task PostProcessDataAsync(ProtocolPackage package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (DataAccumulator is null) return;

        if (!DataAccumulator.Window.IsReady()) return;
        
        context.DataChannel.Writer.TryWrite(DataAccumulator.FlushWindow());

        var header = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckPackageBody(package.Header.Sequence);
        
        var responsePackage = new ProtocolPackage(header, ack);

        await context.Session.SendAsync(responsePackage, cancellationToken);

        if (!DataAccumulator.IsComplete())
        {
            DataAccumulator.UpdateWindow();
        }
        else
        {
            DataAccumulator.Dispose();
            DataAccumulator = null;
        }
    }

    public void Dispose()
    {
        DataAccumulator?.Dispose();
    }
}

internal sealed class PackageTracker
{
    public IPackageBody? LastReceivedPackage { get; set; }
    public ProtocolPackage? LastSentPackage { get; set; }
}

internal sealed record SessionInfo(Guid SessionId)
{
    public bool IsHandshake { get; set; }
    public bool  IsAborted { get; set; }
    public bool IsStarted { get; set; }
}

internal sealed class SessionDataPipeline : IDisposable
{
    private readonly Channel<byte[]> _channel =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

    public ChannelReader<byte[]> Reader => _channel.Reader;
    public ChannelWriter<byte[]> Writer => _channel.Writer;

    public void Dispose() => _channel.Writer.TryComplete();
}

internal sealed class SequenceManager
{
    private volatile uint _expectedSequence = uint.MinValue;
    private uint _currentSequence = uint.MaxValue;

    public uint Expected => _expectedSequence;

    public uint GetNextOutgoing() => 
        Interlocked.Increment(ref _currentSequence);

    public void AdvanceExpected() => 
        Interlocked.Increment(ref _expectedSequence);
}