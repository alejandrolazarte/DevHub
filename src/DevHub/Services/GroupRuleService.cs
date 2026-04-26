using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public class GroupRuleService(IDbContextFactory<ApplicationDbContext> dbFactory) : IGroupRuleService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;

    public async Task<IReadOnlyList<GroupRule>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.GroupRules.OrderBy(r => r.Order).ToListAsync(ct);
    }

    public async Task<GroupRule?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.GroupRules.FindAsync([id], ct);
    }

    public async Task<GroupRule> CreateAsync(GroupRule rule, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var maxOrder = await db.GroupRules.MaxAsync(r => (int?)r.Order, ct) ?? -1;
        rule.Order = maxOrder + 1;
        db.GroupRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<GroupRule> UpdateAsync(GroupRule rule, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.GroupRules.FindAsync([rule.Id], ct)
            ?? throw new KeyNotFoundException($"GroupRule {rule.Id} not found");
        existing.Name = rule.Name;
        existing.Color = rule.Color;
        existing.Prefixes = rule.Prefixes;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rule = await db.GroupRules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"GroupRule {id} not found");
        db.GroupRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(int[] orderedIds, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rules = await db.GroupRules.ToListAsync(ct);
        var lookup = rules.ToDictionary(r => r.Id);
        for (int i = 0; i < orderedIds.Length; i++)
        {
            if (lookup.TryGetValue(orderedIds[i], out var rule))
            {
                rule.Order = i;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
