using DevHub.Models;

namespace DevHub.Services;

public static class GroupRuleCache
{
    private static List<GroupRule> _rules = [];
    private static string _defaultGroup = "Other";

    public static void Initialize(IReadOnlyList<GroupRule> rules, string defaultGroup)
    {
        _rules = [.. rules];
        _defaultGroup = defaultGroup;
    }

    public static IReadOnlyList<GroupRule> GetRules() => _rules;

    public static string GetDefaultGroup() => _defaultGroup;
}
