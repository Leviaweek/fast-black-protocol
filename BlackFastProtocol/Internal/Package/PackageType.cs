namespace BlackFastProtocol.Internal.Package;

internal enum PackageType : byte
{
    Handshake = 0,
    Ack = 1,
    UnAck = 2,
    DataHeader = 3,
    Data = 4,
    Ping = 5,
    Pong = 6,
}