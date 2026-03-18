namespace BlackFastProtocol.Internal.Package.Interfaces;

internal interface IWriteableData
{
    internal int WriteData(Span<byte> buffer, int offset = 0);
}