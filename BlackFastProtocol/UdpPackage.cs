using System.Buffers;
using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class UdpPackage(PackageHeader header, IMemoryOwner<byte> owner, int length) : IDisposable
{
    public PackageHeader Header { get; } = header;
    public ReadOnlyMemory<byte> Data { get; } = owner.Memory[..length];

    public void Dispose() => owner.Dispose();
}