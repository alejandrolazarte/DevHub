namespace DevHub.Models;

public class CanvasBoard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string CytoscapeJson { get; set; } = "{}";
}
