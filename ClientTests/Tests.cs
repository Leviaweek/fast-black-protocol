using System.Net;
using BlackFastProtocol;

namespace ClientTests;

public class Tests
{
    private BlackFastListener _listener;
    private CancellationTokenSource _cts;
    private BlackFastUserClient _client;
    [SetUp]
    public void Setup()
    {
        _listener = new BlackFastListener(new IPEndPoint(IPAddress.Loopback, 12345));
        _client = new BlackFastUserClient(new IPEndPoint(IPAddress.Loopback, 12344));
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
            await _client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 12345), _cts.Token);
            var client = await task;
            //get last received client package by reflection
            
            await Task.Delay(3000);
            
            var context = (FastBlackSessionContext)_client.GetType().GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_client);
            
            Console.WriteLine(context?.LastReceivedPackage);
            
            Assert.That(client, Is.Not.Null);
        
    }

    [Test]
    public async Task TestStream()
    {
        _ = _listener.StartAsync(_cts.Token);
        var task = _listener.AcceptClientAsync(_cts.Token);
        await _client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 12345), _cts.Token);
        var client = await task;
        
        await _client.SendAsync(new byte[] { 1, 2, 3, 4 }, _cts.Token);
        
        var data = await client.ReceiveAsync(_cts.Token);
        
        Assert.That(data, Is.Not.Null);
        Assert.That(data, Has.Length.EqualTo(4));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data[0], Is.EqualTo(1));
            Assert.That(data[1], Is.EqualTo(2));
            Assert.That(data[2], Is.EqualTo(3));
            Assert.That(data[3], Is.EqualTo(4));
        }
    }
}