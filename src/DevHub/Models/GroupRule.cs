namespace DevHub.Models;

public class GroupRule
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "default";
    public List<string> Prefixes { get; set; } = [];
}
