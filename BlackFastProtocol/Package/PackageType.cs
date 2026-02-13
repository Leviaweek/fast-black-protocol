namespace UdpServer.Packages;

public enum PackageType : byte
{
    Handshake = 0,
    Ack = 1,
    UnAck = 2,
    DataHeader = 3,
    DataFrame = 4
}