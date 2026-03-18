namespace BlackFastProtocol.Package;

internal interface IReadableData<out T>
{
    internal static abstract T ReadData(ReadOnlyMemory<byte> buffer, int offset = 0);
}