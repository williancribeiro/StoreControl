namespace StoreControl.Infrastructure.Messaging;

internal class PermanentMessageException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class MessageIdentityCollisionException(Guid messageId)
    : PermanentMessageException($"Message ID {messageId:D} was already processed with different content.");
