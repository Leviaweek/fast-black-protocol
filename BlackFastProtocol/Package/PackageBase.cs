namespace BlackFastProtocol.Package;

public abstract record PackageBase(PackageType Type, int Id, int Length): ITypedPackage;