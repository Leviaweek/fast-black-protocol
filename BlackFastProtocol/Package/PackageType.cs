namespace BlackFastProtocol.Package;

public enum PackageType : byte
{
    Handshake = 0,
    Ack = 1,
    UnAck = 2,
    DataPackage = 3,
    DataHeader = 4,
    DataFrame = 5,
    DataChunk = 6,
}