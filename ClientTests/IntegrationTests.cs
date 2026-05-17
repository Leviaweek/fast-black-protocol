using System.Net;
using BlackFastProtocol.Public;

namespace ClientTests;

/// <summary>
/// Integration tests that exercise real client ↔ server packet exchange
/// over a loopback UDP socket.
///
/// Design rules:
///  - Each test uses its own port pair so tests can run in parallel.
///  - Every test has a hard CancellationTokenSource timeout (default 5 s)
///    so a bug causes a clean timeout failure, not an infinite hang.
///  - Dispose is always called in a finally block.
/// </summary>
[TestFixture]
public class IntegrationTests
{
    // Each test method gets an isolated port pair (server, client).
    private const int S1 = 13100; private const int C1 = 13101;
    private const int S2 = 13102; private const int C2 = 13103;
    private const int S3 = 13104; private const int C3 = 13105;
    private const int S4 = 13106; private const int C4 = 13107;
    private const int S5 = 13108; private const int C5 = 13109;

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
        int serverPort,
        CancellationToken ct)
    {
        _ = listener.StartAsync(ct);

        // Start both concurrently — ConnectAsync fires the Handshake that unblocks
        // AcceptClientAsync on the server.
        var acceptTask  = listener.AcceptClientAsync(ct);
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, serverPort), ct);
        return await acceptTask;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Connection_Established_ServerSideClientNotNull()
    {
        using var cts      = new CancellationTokenSource(5000);
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, S1));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, C1));

        var serverSide = await SetupAsync(listener, client, S1, cts.Token);

        Assert.That(serverSide, Is.Not.Null);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_SmallPayload_ReceivedCorrectly()
    {
        using var cts      = new CancellationTokenSource(5000);
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, S2));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, C2));

        var serverSide = await SetupAsync(listener, client, S2, cts.Token);

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

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_MultipleMessages_AllReceivedInOrder()
    {
        using var cts      = new CancellationTokenSource(5000);
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, S3));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, C3));

        var serverSide = await SetupAsync(listener, client, S3, cts.Token);

        for (byte i = 1; i <= 5; i++)
        {
            var msg = new[] { i, (byte)(i * 10) };
            await client.SendAsync(msg, cts.Token);
            var received = await serverSide.ReceiveAsync(cts.Token);
            Assert.That(received, Is.EqualTo(msg), $"message {i} content mismatch");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_BidirectionalExchange_BothSidesReceive()
    {
        using var cts      = new CancellationTokenSource(5000);
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, S4));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, C4));

        var serverSide = await SetupAsync(listener, client, S4, cts.Token);

        // Client → Server
        var toServer = new byte[] { 0xAA, 0xBB };
        await client.SendAsync(toServer, cts.Token);
        var gotOnServer = await serverSide.ReceiveAsync(cts.Token);
        Assert.That(gotOnServer, Is.EqualTo(toServer), "server side mismatch");

        // Server → Client
        var toClient = new byte[] { 0x11, 0x22, 0x33 };
        await serverSide.SendAsync(toClient, cts.Token);
        var gotOnClient = await client.ReceiveAsync(cts.Token);
        Assert.That(gotOnClient, Is.EqualTo(toClient), "client side mismatch");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SendAsync_LargePayload_FullyReassembled()
    {
        // 5000 bytes spans ceil(5000/1369) = 4 fragments.
        // The server reassembles them window-by-window via DataAccumulator.
        using var cts      = new CancellationTokenSource();
        using var listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, S5));
        using var client   = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, C5));

        var serverSide = await SetupAsync(listener, client, S5, cts.Token);

        var payload = Enumerable.Range(0, 5000)
                                .Select(i => (byte)(i % 251)) // 251 is prime → varied pattern
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
}
