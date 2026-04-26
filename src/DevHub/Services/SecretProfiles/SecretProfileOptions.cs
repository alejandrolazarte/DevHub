namespace DevHub.Services.SecretProfiles;

public class SecretProfileOptions
{
    public string ProfilesRoot { get; set; } = @"..\..\profiles";
    public List<SecretProfileServiceConfig> Services { get; set; } = [];
}

public class SecretProfileServiceConfig
{
    public string Name { get; set; } = "";
    public string UserSecretsId { get; set; } = "";
    public List<string> ProdProfileNames { get; set; } = [];
}
