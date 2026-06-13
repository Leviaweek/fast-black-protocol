using System.Net;
using BlackFastProtocol.Public;

namespace ClientTests;

/// <summary>
/// Integration tests that exercise real client ↔ server packet exchange
/// over a loopback UDP socket.
///
/// Design rules:
///  - Each test asks the OS for isolated loopback UDP ports.
///  - Setup has a hard timeout so a broken handshake causes a clean failure,
///    not an infinite hang.
///  - Async disposal is awaited in finally blocks so background loops do not
///    leak into the next test.
/// </summary>
[TestFixture]
public class IntegrationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    // ── Shared setup helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Starts the listener, connects the client, and returns the server-side client.
    /// Both AcceptClientAsync and ConnectAsync are started concurrently because each
    /// one waits for the other:
    ///   - AcceptClientAsync blocks until the first packet from the client arrives.
    ///   - ConnectAsync sends the Handshake which triggers AcceptClientAsync to return.
    /// </summary>
    private static async Task<BlackFastClient> SetupAsync(
        BlackFastListener listener,
        BlackFastUserClient client,
        CancellationToken ct)
    {
        var listenerTask = listener.StartAsync(ct);

        // Start both concurrently — ConnectAsync fires the Handshake that unblocks
        // AcceptClientAsync on the server.
        var acceptTask  = listener.AcceptClientAsync(ct);
        await client.ConnectAsync(listener.EndPoint, ct).WaitAsync(TestTimeout);
        var serverSide = await acceptTask.WaitAsync(TestTimeout);

        if (listenerTask.IsFaulted)
            await listenerTask;

        return serverSide;
    }

    private static async Task<(BlackFastListener Listener, BlackFastUserClient Client, BlackFastClient ServerSide)>
        ConnectPairAsync(CancellationToken ct)
    {
        var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 0));
        var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 0));

        try
        {
            var serverSide = await SetupAsync(listener, client, ct);
            return (listener, client, serverSide);
        }
        catch
        {
            client.Dispose();
            listener.Dispose();
            throw;
        }
    }

    private static async ValueTask DisposeServerSideAsync(BlackFastClient? serverSide)
    {
        if (serverSide is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (serverSide is IDisposable disposable)
            disposable.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Connection_Established_ServerSideClientNotNull()
    {
        using var cts      = new CancellationTokenSource();
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 0));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 0));
        BlackFastClient? serverSide = null;

        try
        {
            serverSide = await SetupAsync(listener, client, cts.Token);

            Assert.That(serverSide, Is.Not.Null);
        }
        finally
        {
            await DisposeServerSideAsync(serverSide);
            await client.DisposeAsync();
            await listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_SmallPayload_ReceivedCorrectly()
    {
        using var cts      = new CancellationTokenSource();
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 0));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 0));
        BlackFastClient? serverSide = null;

        try
        {
            serverSide = await SetupAsync(listener, client, cts.Token);

            var payload = new byte[] { 1, 2, 3, 4 };
            await client.SendAsync(payload, cts.Token);

            var received = await serverSide.ReceiveAsync(cts.Token);

            Assert.That(received, Has.Length.EqualTo(4));
            Assert.Multiple(() =>
            {
                Assert.That(received[0], Is.EqualTo(1));
                Assert.That(received[1], Is.EqualTo(2));
                Assert.That(received[2], Is.EqualTo(3));
                Assert.That(received[3], Is.EqualTo(4));
            });
        }
        finally
        {
            await DisposeServerSideAsync(serverSide);
            await client.DisposeAsync();
            await listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_MultipleMessages_AllReceivedInOrder()
    {
        using var cts      = new CancellationTokenSource();
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 0));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 0));
        BlackFastClient? serverSide = null;

        try
        {
            serverSide = await SetupAsync(listener, client, cts.Token);

            for (byte i = 1; i <= 5; i++)
            {
                var msg = new[] { i, (byte)(i * 10) };
                await client.SendAsync(msg, cts.Token);
                var received = await serverSide.ReceiveAsync(cts.Token);
                Assert.That(received, Is.EqualTo(msg), $"message {i} content mismatch");
            }
        }
        finally
        {
            await DisposeServerSideAsync(serverSide);
            await client.DisposeAsync();
            await listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_BidirectionalExchange_BothSidesReceive()
    {
        using var cts      = new CancellationTokenSource();
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 0));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 0));
        BlackFastClient? serverSide = null;

        try
        {
            serverSide = await SetupAsync(listener, client, cts.Token);

            // Client -> Server
            var toServer = new byte[] { 0xAA, 0xBB };
            await client.SendAsync(toServer, cts.Token);
            var gotOnServer = await serverSide.ReceiveAsync(cts.Token);
            Assert.That(gotOnServer, Is.EqualTo(toServer), "server side mismatch");

            // Server -> Client
            var toClient = new byte[] { 0x11, 0x22, 0x33 };
            await serverSide.SendAsync(toClient, cts.Token);
            var gotOnClient = await client.ReceiveAsync(cts.Token);
            Assert.That(gotOnClient, Is.EqualTo(toClient), "client side mismatch");
        }
        finally
        {
            await DisposeServerSideAsync(serverSide);
            await client.DisposeAsync();
            await listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_LargePayload_FullyReassembled()
    {
        // 5000 bytes spans ceil(5000/1369) = 4 fragments.
        // The server reassembles them window-by-window via DataAccumulator.
        using var cts      = new CancellationTokenSource();
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 0));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 0));
        BlackFastClient? serverSide = null;

        try
        {
            serverSide = await SetupAsync(listener, client, cts.Token);

            var payload = Enumerable.Range(0, 5000)
                                    .Select(i => (byte)(i % 251)) // 251 is prime -> varied pattern
                                    .ToArray();

            await client.SendAsync(payload, cts.Token);

            // Large payloads may be delivered in multiple ReceiveAsync calls (one per window).
            var received = new List<byte>(payload.Length);
            while (received.Count < payload.Length)
            {
                var chunk = await serverSide.ReceiveAsync(cts.Token);
                received.AddRange(chunk);
            }

            Assert.That(received.ToArray(), Is.EqualTo(payload));
        }
        finally
        {
            await DisposeServerSideAsync(serverSide);
            await client.DisposeAsync();
            await listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }

    [Test]
    public async Task ReceiveAsync_CanceledBeforeData_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var pair = await ConnectPairAsync(cts.Token);

        try
        {
            using var receiveCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await pair.ServerSide.ReceiveAsync(receiveCts.Token));
        }
        finally
        {
            await DisposeServerSideAsync(pair.ServerSide);
            await pair.Client.DisposeAsync();
            await pair.Listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }

    [Test]
    public async Task SendAsync_ParallelIndependentExchanges_AllReceiveExpectedPayloads()
    {
        using var cts = new CancellationTokenSource();

        async Task ExchangeAsync(byte value)
        {
            var pair = await ConnectPairAsync(cts.Token);
            try
            {
                var payload = new[] { value, (byte)(value + 1) };
                await pair.Client.SendAsync(payload, cts.Token);
                var received = await pair.ServerSide.ReceiveAsync(cts.Token);
                Assert.That(received, Is.EqualTo(payload));
            }
            finally
            {
                await DisposeServerSideAsync(pair.ServerSide);
                await pair.Client.DisposeAsync();
                await pair.Listener.DisposeAsync();
            }
        }

        await Task.WhenAll(
            ExchangeAsync(10),
            ExchangeAsync(20),
            ExchangeAsync(30));

        await cts.CancelAsync();
    }

    [Test]
    public async Task SendAsync_ServerSideDisposedBeforeSend_ThrowsOrCancels()
    {
        using var cts = new CancellationTokenSource();
        var pair = await ConnectPairAsync(cts.Token);

        try
        {
            await DisposeServerSideAsync(pair.ServerSide);

            Assert.That(
                async () => await pair.ServerSide.SendAsync(new byte[] { 1, 2, 3 }, cts.Token),
                Throws.InstanceOf<Exception>());
        }
        finally
        {
            await pair.Client.DisposeAsync();
            await pair.Listener.DisposeAsync();
            await cts.CancelAsync();
        }
    }
}
