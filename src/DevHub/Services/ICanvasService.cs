using DevHub.Models;

namespace DevHub.Services;

public interface ICanvasService
{
    Task<IReadOnlyList<CanvasBoard>> GetAllAsync(CancellationToken ct = default);
    Task<CanvasBoard?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CanvasBoard> CreateAsync(string name, CancellationToken ct = default);
    Task SaveGraphAsync(int canvasId, string cytoscapeJson, CancellationToken ct = default);
    Task RenameAsync(int canvasId, string newName, CancellationToken ct = default);
    Task DeleteAsync(int canvasId, CancellationToken ct = default);
}
