namespace BlackFastProtocol.Package;

internal interface IWriteableData
{
    internal int WriteData(Span<byte> buffer, int offset = 0);
}