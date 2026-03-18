namespace BlackFastProtocol;

internal sealed class SessionInfo(Guid sessionId)
{
    public Guid SessionId { get; } = sessionId;
    public bool IsHandshake { get; set; }
    public bool  IsAborted { get; set; }
    public bool IsStarted { get; set; }
}