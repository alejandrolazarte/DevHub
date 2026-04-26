using DevHub.Models;

namespace DevHub.Services;

public class DevHubOptions
{
    public string RootPath { get; set; } = @"C:\repos";
    public int FetchIntervalMinutes { get; set; } = 5;
    public int ParallelScanDegree { get; set; } = 8;
    public List<string> ExcludedRepos { get; set; } = [];
    public List<GroupRule> Groups { get; set; } = [];
    public string DefaultGroup { get; set; } = "Other";
}
