namespace UdpServer.Packages;

public abstract record Package(PackageType Type, int Id, int Length): ITypedPackage;