namespace BlackFastProtocol.Internal.Package;

internal enum PackageType : byte
{
    Handshake = 0,
    Ack = 1,
    DataHeader = 2,
    Data = 3,
    Ping = 4,
    Pong = 5,
}