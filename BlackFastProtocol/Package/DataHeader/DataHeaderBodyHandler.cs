namespace BlackFastProtocol.Package.DataHeader;

public sealed class DataHeaderBodyHandler : IBodyHandler<DataHeaderBody>
{
    public Task HandlePackageAsync(DataHeaderBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.IsHandshake)
            return Task.CompletedTask;

        context.DataAccumulator = new DataAccumulator(package.DataLength);
        return Task.CompletedTask;
    }

    public void HandlePackage(DataHeaderBody package, FastBlackSessionContext context)
    {
        context.DataAccumulator = new DataAccumulator(package.DataLength);
    }
}