namespace UdpServer.Packages;

public interface IWriteablePackage
{
    public int ToBytes(Span<byte> buffer);
}