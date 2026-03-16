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
    internal bool IsStarted { get; private set; }
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
    
    internal void Start() => IsStarted = true;
    
    internal void AdvanceExpectedSequence() => Interlocked.Increment(ref _expectedSequence);

    public uint GetNextSequence() => Interlocked.Increment(ref _currentSequence);
    public Guid SessionId { get; } = sessionId;

    public Channel<byte[]> DataChannel { get; } = Channel.CreateUnbounded<byte[]>(
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
            if (!ReorderingBuffer.TryGetOrderedPackage(package.Header.Sequence, out var orderedPackage))
            {
                return;
            }

            if (!IsStarted)
            {
                return;
            }
            
            await HandleAllPackageAsync(orderedPackage!, cancellationToken);
            return;
        }
        
        if (!IsStarted)
            return;
        
        await HandleAllPackageAsync(package, cancellationToken);
    }

    private async Task HandleAllPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        await _clientState.HandleAsync(package, this, cancellationToken);

        if (IsAborted)
        {
            Dispose();
            return;
        }
        
        while (ReorderingBuffer.TryGetOrderedPackage(_expectedSequence, out var orderedPackage))
        {
            await _clientState.HandleAsync(orderedPackage!, this, cancellationToken);
            
            if (IsAborted)
            {
                Dispose();
                return;
            }
        }
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
    }
}

internal sealed class StreamClientState(int dataLength = 0) : ClientState, IDisposable
{
    internal DataAccumulator? DataAccumulator { get; private set; } = dataLength > 0 ? new DataAccumulator(dataLength) : null;

    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.AdvanceExpectedSequence();
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