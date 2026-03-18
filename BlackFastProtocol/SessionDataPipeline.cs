using System.Threading.Channels;

namespace BlackFastProtocol;

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