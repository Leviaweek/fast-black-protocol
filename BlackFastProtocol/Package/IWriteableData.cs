namespace BlackFastProtocol.Package;

public interface IWriteableData
{
    public int WriteData(Span<byte> buffer, int offset = 0);
}