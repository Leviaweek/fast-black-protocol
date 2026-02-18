namespace BlackFastProtocol.Package;

public interface IWriteableData
{
    public int ToBytes(Span<byte> buffer, int offset = 0);
}