using Microsoft.EntityFrameworkCore;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

internal static class CollectionImportAssignment
{
    public static async Task<Dictionary<Guid, Guid?>> ActiveTeamIdsAsync(ApplicationDbContext db, CancellationToken token)
    {
        var rows = await db.CollectionTeamMembers.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.UserId, x.TeamId }).ToArrayAsync(token);
        return rows.GroupBy(x => x.UserId).ToDictionary(group => group.Key, group => (Guid?)group.First().TeamId);
    }

    public static bool TryAssign(ApplicationDbContext db, CollectionCase item, IReadOnlyDictionary<Guid, Guid?> teams, Guid assignedById, DateTimeOffset now)
    {
        var teamId = item.FileCollectorUserId is Guid collectorId && teams.TryGetValue(collectorId, out var mapped) ? mapped : null;
        if (!item.TryAssignImportedFileCollector(teamId, now) || item.AssignedCollectorId is not Guid assignedTo) return false;
        db.CollectionAssignmentHistory.Add(new CollectionAssignmentHistory(item.Id, null, assignedTo, assignedById, item.AssignedTeamId, "Assigned from imported file collector", CollectionsValues.AssignmentSources.Import, "FILE_COLLECTOR", now));
        db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Assignment, "FILE_ASSIGNED", "Assigned from imported file collector", null, assignedById, now, null));
        return true;
    }
}
