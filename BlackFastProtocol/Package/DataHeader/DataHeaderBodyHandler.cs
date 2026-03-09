namespace BlackFastProtocol.Package.DataHeader;

public sealed class DataHeaderBodyHandler : IBodyHandler<DataHeaderBody>
{
    public Task HandlePackageAsync(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.IsHandshake)
            return Task.CompletedTask;

        context.ClientState = new StreamClientState(package.DataLength);
        return Task.CompletedTask;
    }

    public void HandlePackage(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context)
    {
        if (!context.IsHandshake)
            return;
    
        context.ClientState = new StreamClientState(package.DataLength);
    }
}