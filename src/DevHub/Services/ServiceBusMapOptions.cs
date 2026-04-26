namespace DevHub.Services;

public class ServiceBusMapOptions
{
    public string ScriptPath { get; set; } = @"..\..\scripts\generate-servicebus-map.ps1";
    public string TemplateFile { get; set; } = @"wwwroot\maps\servicebus-map.template.html";
    public string OutputFile { get; set; } = @"wwwroot\maps\servicebus-map.html";
    public string ReposRoot { get; set; } = @"C:\repos";
    public string RepositoryNamePattern { get; set; } = ".*";
    public string DisplayNamePrefixToTrim { get; set; } = string.Empty;
}
