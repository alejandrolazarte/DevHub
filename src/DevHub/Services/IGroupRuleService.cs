using DevHub.Models;

namespace DevHub.Services;

public interface IGroupRuleService
{
    Task<IReadOnlyList<GroupRule>> GetAllAsync(CancellationToken ct = default);
    Task<GroupRule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<GroupRule> CreateAsync(GroupRule rule, CancellationToken ct = default);
    Task<GroupRule> UpdateAsync(GroupRule rule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task ReorderAsync(int[] orderedIds, CancellationToken ct = default);
}
