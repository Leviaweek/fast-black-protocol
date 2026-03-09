using System.Threading.Channels;
using BlackFastProtocol.Package;

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
    private readonly DataAccumulator _dataAccumulator = new(dataLength);

    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        
    }

    public void Dispose()
    {
        
    }
}