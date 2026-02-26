using System.Net;
using System.Net.Sockets;
using System.Text;
using BlackFastProtocol;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;
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
    public async Task TestConnection()
    {
            _ = _listener.StartAsync(_cts.Token);
            var task = _listener.AcceptClientAsync(_cts.Token);
            var header = new PackageHeader(Guid.NewGuid(), PackageType.Handshake, int.MinValue);
            var body = new HandshakeBody();
            var buffer = new byte[body.Length + header.Length];
            header.WriteData(buffer);
            body.WriteData(buffer, header.Length);
            await _client.SendAsync(buffer, new IPEndPoint(IPAddress.Loopback, 12345));
            var client = await task;
            var response = await _client.ReceiveAsync();
            var responseBuffer = response.Buffer;
            var responseHeader = PackageHeader.ReadData(responseBuffer);
            var responseBody = HandshakeBody.ReadData(responseBuffer);
            Assert.That(responseHeader.Sequence, Is.EqualTo(header.Sequence + 1));
        
    }

    [Test]
    public async Task TestDataPackage()
    {
        _ = _listener.StartAsync(_cts.Token);
        var task = _listener.AcceptClientAsync(_cts.Token);
        var header = new PackageHeader(Guid.NewGuid(), PackageType.Handshake, int.MinValue);
        var body = new HandshakeBody();
        var buffer = new byte[body.Length + header.Length];
        header.WriteData(buffer);
        body.WriteData(buffer, header.Length);
        await _client.SendAsync(buffer, new IPEndPoint(IPAddress.Loopback, 12345));
        var client = await task;
        var response = await _client.ReceiveAsync();
        var responseBuffer = response.Buffer;
        var responseHeader = PackageHeader.ReadData(responseBuffer);
        var responseBody = HandshakeBody.ReadData(responseBuffer);

        var newHeader = new PackageHeader(responseHeader.SessionId, PackageType.DataPackage, responseHeader.Sequence + 1);
        var str = Encoding.UTF8.GetBytes("Hello world");
        var newBody = new DataPackageBody(str);
    }
}