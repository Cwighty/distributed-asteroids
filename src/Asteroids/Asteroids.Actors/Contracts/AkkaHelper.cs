using System.Text.RegularExpressions;

namespace Asteroids.Shared.Contracts;

public static class AkkaHelper
{
    public static string UsernameToActorPath(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        }

        // Replace invalid characters with underscores
        // and make the string lowercase to ensure consistency.
        string validActorPath = Regex.Replace(username, "[\\$\\/\\#\\s]+", "_").ToLower();

        return validActorPath;
    }

    public static string AccountSupervisorActorPath => "account-supervisor";
    public static string AccountStateActorPath => "account-state";
    public static string UserSessionSupervisorActorPath => "user-session-supervisor";
}
