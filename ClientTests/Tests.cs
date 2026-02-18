using System.Net;
using System.Net.Sockets;
using BlackFastProtocol;
using BlackFastProtocol.Package.Handshake;

namespace ClientTests;

public class Tests
{
    private BlackFastListener _listener;
    private UdpClient _client;
    private CancellationTokenSource _cts;
    [SetUp]
    public void Setup()
    {
        _listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 12345));
        _client = new UdpClient(12344);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
        _client.Dispose();
        _cts.Dispose();
    }

    [Test]
    public async Task Test1()
    {
            _ = _listener.StartAsync(_cts.Token);
            var task = _listener.AcceptClientAsync(_cts.Token);
            var handshakePackage = new HandshakeBody(Guid.NewGuid(), int.MinValue);
            var buffer = new byte[handshakePackage.Length];
            handshakePackage.ToBytes(buffer);
            await _client.SendAsync(buffer, new IPEndPoint(IPAddress.Loopback, 12345));
            var client = await task;
            var response = await _client.ReceiveAsync();
            var responsePackage = HandshakeBody.ReadPackage(response.Buffer);
            Assert.That(responsePackage.Header.Id, Is.EqualTo(handshakePackage.Header.Id + 1));
        
    }
}