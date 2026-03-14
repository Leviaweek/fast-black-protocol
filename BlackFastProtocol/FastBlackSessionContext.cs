using System.Threading.Channels;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.Ack;

namespace BlackFastProtocol;

public sealed class FastBlackSessionContext(
    BlackFastClient client,
    Guid sessionId) : IDisposable
{
    public BlackFastClient Session { get; } = client;
    public bool IsAborted { get; set; }
    public bool IsHandshake { get; set; }
    public IPackageBody? LastReceivedPackage { get; set; }
    public ProtocolPackage? LastSentPackage { get; set; }
    private volatile uint _expectedSequence = uint.MinValue;

    private uint _currentSequence = uint.MaxValue;
    internal ReorderingBuffer ReorderingBuffer { get; } = new();
    
    
    private volatile ClientState _clientState = new DefaultClientState();
    internal uint ExpectedSequence => _expectedSequence;
    
    internal ClientState ClientState
    {
        get => _clientState;
        set => Interlocked.Exchange(ref _clientState, value);
    }
    
    internal void AdvanceExpectedSequence() => Interlocked.Increment(ref _expectedSequence);

    public uint GetNextSequence() => Interlocked.Increment(ref _currentSequence);
    public Guid SessionId { get; } = sessionId;

    public Channel<ReadOnlyMemory<byte>> DataChannel { get; } = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

    public async Task HandlePackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var diff = (int)package.Header.Sequence - _expectedSequence;
        if (diff < 0 || diff >= ReorderingBuffer.Length)
        {
            return;
        }
        
        if (package.Header.Sequence != _expectedSequence)
        {
            if (!ReorderingBuffer.TryAdd(package))
            {
                throw new ArgumentException("Incorrect argument", nameof(package));
            }

            return;
        }
        
        await _clientState.HandleAsync(package, this, cancellationToken);

        if (!IsAborted) return;
        Dispose();
    }

    public void Dispose()
    {
        DataChannel.Writer.TryComplete();
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
        context.AdvanceExpectedSequence();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!context.ReorderingBuffer.TryGetOrderedPackage(context.ExpectedSequence, out var orderedPackage))
            {
                return;
            }
            context.AdvanceExpectedSequence();
            await PackageHelper.Handlers[orderedPackage!.Header.Type].HandlePackageAsync(orderedPackage, context, cancellationToken);
        }
    }
}

internal sealed class StreamClientState(int dataLength) : ClientState, IDisposable
{
    internal DataAccumulator? DataAccumulator { get; private set; } = new(dataLength);

    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.AdvanceExpectedSequence();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);

        await PostProcessDataAsync(package, context, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!context.ReorderingBuffer.TryGetOrderedPackage(context.ExpectedSequence, out var orderedPackage))
            {
                return;
            }
            context.AdvanceExpectedSequence();
            await PackageHelper.Handlers[orderedPackage!.Header.Type].HandlePackageAsync(orderedPackage, context, cancellationToken);
            await PostProcessDataAsync(package, context, cancellationToken);
        }
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