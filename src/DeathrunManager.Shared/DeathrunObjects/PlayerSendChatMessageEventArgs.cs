namespace DeathrunManager.Shared.DeathrunObjects;

/// <summary>
/// Event args passed when a deathrun player sends a chat message.
/// Subscribers may modify <see cref="Message"/> to change what is ultimately printed to chat.
/// </summary>
public class PlayerSendChatMessageEventArgs(string message)
{
    /// <summary>
    /// The fully assembled chat message (prefix + body) before any color codes are processed.
    /// Modify this value to change what is printed to chat.
    /// </summary>
    public string Message { get; set; } = message;
}
