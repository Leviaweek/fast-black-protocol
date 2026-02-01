using System.Buffers;

namespace BlackFastProtocol;

public readonly struct UdpPackage(IMemoryOwner<byte> owner, int length) : IDisposable
{
    public ReadOnlyMemory<byte> Data { get; } = owner.Memory[..length];

    public void Dispose() => owner.Dispose();
}