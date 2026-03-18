namespace BlackFastProtocol.Package.DataHeader;

internal sealed class DataHeaderBodyHandler : IBodyHandler<DataHeaderBody>
{
    public Task HandlePackageAsync(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Info.IsHandshake)
            return Task.CompletedTask;

        context.ClientState = new StreamClientState(package.DataLength);
        return Task.CompletedTask;
    }

    public void HandlePackage(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context)
    {
        if (!context.Info.IsHandshake)
            return;
    
        context.ClientState = new StreamClientState(package.DataLength);
    }
}