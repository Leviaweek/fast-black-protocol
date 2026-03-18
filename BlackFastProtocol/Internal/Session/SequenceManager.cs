namespace BlackFastProtocol.Internal.Session;

internal sealed class SequenceManager
{
    private volatile uint _expectedSequence = uint.MinValue;
    private uint _currentSequence = uint.MaxValue;

    public uint Expected => _expectedSequence;

    public uint GetNextOutgoing() => 
        Interlocked.Increment(ref _currentSequence);

    public void AdvanceExpected() => 
        Interlocked.Increment(ref _expectedSequence);
}