namespace DevHub.Services.SecretProfiles;

public record ServiceProfileView(string Name, string UserSecretsId);

public record ProfileInfo(string Name, bool IsProd, long SizeBytes, DateTime ModifiedUtc);

public record ActiveProfileInfo(string? MatchedProfileName, bool IsDirty, bool IsProd, bool LiveSecretsExists);

public class SecretProfileException(string message) : Exception(message);
public class ProdConfirmationRequiredException(string profileName) : SecretProfileException($"Profile '{profileName}' is marked as prod and requires explicit confirmation.");
public class InvalidProfileNameException(string name) : SecretProfileException($"Invalid profile name '{name}'. Allowed characters: A-Z a-z 0-9 . _ -");
public class ProfileNotFoundException(string name) : SecretProfileException($"Profile '{name}' not found.");
public class ServiceNotConfiguredException(string name) : SecretProfileException($"Service '{name}' not configured in SecretProfiles.Services.");
public class LiveSecretsMissingException(string userSecretsId) : SecretProfileException($"Live secrets.json does not exist for UserSecretsId '{userSecretsId}'.");
public class CannotDeleteActiveProfileException(string name) : SecretProfileException($"Cannot delete profile '{name}' because it is currently active.");
