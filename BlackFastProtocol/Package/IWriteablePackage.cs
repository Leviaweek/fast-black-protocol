namespace BlackFastProtocol.Package;

public interface IWriteablePackage
{
    public int ToBytes(Span<byte> buffer);
}