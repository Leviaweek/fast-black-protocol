namespace BlackFastProtocol.Package.DataPackage;

public class DataPackageBodyHandler: IBodyHandler<DataPackageBody>
{
    public async Task HandlePackageAsync(PackageHeader header, DataPackageBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ClientState is not StreamClientState streamClientState)
        {
            return;
        }

        if (streamClientState.DataAccumulator is null)
            await context.DataChannel.Writer.WriteAsync(package.Data, cancellationToken);
        else
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
    }

    public void HandlePackage(PackageHeader header, DataPackageBody package, FastBlackSessionContext context)
    {
        if (context.ClientState is not StreamClientState streamClientState)
        {
            return;
        }

        if (streamClientState.DataAccumulator is null)
            context.DataChannel.Writer.TryWrite(package.Data);
        else
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
    }
}