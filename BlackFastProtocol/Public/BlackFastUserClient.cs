using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Public;

public sealed class BlackFastUserClient : BlackFastClient, IDisposable, IAsyncDisposable
{
    private protected override FastBlackSessionContext Context { get; }
    private CancellationTokenSource? _runCts;
    private Task? _receiveTask;
    private int _disposed;

    public BlackFastUserClient(IPEndPoint endPoint) : base(new UdpClient(endPoint))
    {
        EndPoint = endPoint;
        Context  = new FastBlackSessionContext(this, Guid.CreateVersion7());
        // NOTE: FastBlackSessionContext constructor already starts SendEngine.RunAsync,
        // so EnqueueAsync is safe to call as soon as this object is constructed.
    }

    public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        Client.Connect(endPoint);

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runToken = _runCts.Token;

        _receiveTask = ReceiveLoop(runToken);
        await Context.StartAsync(runToken);

        Context.Info.IsHandshake = true;

        if (Context.ClientState is not DefaultClientState clientState)
            throw new InvalidOperationException("Unexpected client state after handshake.");

        var handshakeHeader = PackageHeader.CreateFromContext(Context, PackageType.Handshake);
        var handshakePackage = new ProtocolPackage(handshakeHeader, new HandshakeBody());

        while (!clientState.Source.Task.IsCompleted)
        {
            await SendAsync(handshakePackage, runToken);

            var completed = await Task.WhenAny(
                clientState.Source.Task,
                Task.Delay(TimeSpan.FromMilliseconds(100), runToken));

            if (completed == clientState.Source.Task)
                break;
        }

        await clientState.Source.Task.WaitAsync(runToken);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var emptyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer        = new byte[MaxPackageSize];
        var memory        = buffer.AsMemory();

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await Client.Client.ReceiveFromAsync(
                memory, SocketFlags.None, emptyEndpoint, cancellationToken);

            var length = result.ReceivedBytes;
            if (length < PackageHeader.Size) continue;

            if (!PackageHelper.TryReadPackage(memory[..length], out var package))
                continue;

            if (package!.Header.SessionId != Context.Info.SessionId)
                continue;

            await Context.HandlePackageAsync(package, cancellationToken);
        }
    }

    protected override void SendBytes(ReadOnlySpan<byte> buffer)
        => Client.Send(buffer);

    protected override async ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
        => await Client.SendAsync(buffer, ct);

    public override IPEndPoint EndPoint { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _runCts?.Cancel();
        Context.Dispose();
        Client.Dispose();
        _runCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (_runCts is not null)
            await _runCts.CancelAsync();

        Client.Dispose();
        await Context.DisposeAsync();

        if (_receiveTask is not null)
        {
            try { await _receiveTask; }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException) { }
        }

        _runCts?.Dispose();
    }
}
