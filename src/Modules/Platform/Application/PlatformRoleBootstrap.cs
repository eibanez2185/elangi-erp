using System.Net.Mail;

namespace Elangi.Modules.Platform.Application;

public sealed class PlatformRoleBootstrap(IPlatformRoleBootstrapRepository repository)
{
    public Task<Guid> AssignInitialOwnerAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var trimmedEmail = email.Trim();
        try
        {
            var address = new MailAddress(trimmedEmail);
            if (!address.Address.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A valid email address is required.", nameof(email));
            }

            return repository.AssignInitialOwnerAsync(
                address.Address.ToLowerInvariant(),
                cancellationToken);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("A valid email address is required.", nameof(email), exception);
        }
    }
}

public interface IPlatformRoleBootstrapRepository
{
    Task<Guid> AssignInitialOwnerAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);
}

public sealed class PlatformBootstrapException(string message) : InvalidOperationException(message);
