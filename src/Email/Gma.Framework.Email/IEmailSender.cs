namespace Gma.Framework.Email;

public interface IEmailSender
{
    ValueTask<EmailSendResult> SendAsync(
        EmailSendRequest request,
        CancellationToken cancellationToken = default);
}
