using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionImportedCollectorLinkTests
{
    [Fact]
    public void ImportedCollectorNamesLinkWithoutAssigningTheCase()
    {
        var now = DateTimeOffset.UtcNow;
        var fileId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var item = new CollectionCase(Guid.NewGuid(), Guid.NewGuid(), "CASE-1", "ACC-1", 100, 100, 40, 12, Guid.NewGuid(), now);

        item.ApplyImportedDeskFields("أحمد علي", "إسلام محمد", "90+", previousId, fileId);

        Assert.Equal("أحمد علي", item.PreviousCollectorName);
        Assert.Equal("إسلام محمد", item.FileCollectorName);
        Assert.Equal(previousId, item.PreviousCollectorUserId);
        Assert.Equal(fileId, item.FileCollectorUserId);
        Assert.True(item.TryAssignImportedFileCollector(null, now));
        Assert.Equal(fileId, item.AssignedCollectorId);
    }

    [Fact]
    public void AlreadyAssignedCasesKeepTheirCollectorWhenTheFileIsLinked()
    {
        var now = DateTimeOffset.UtcNow;
        var assignedId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var item = new CollectionCase(Guid.NewGuid(), Guid.NewGuid(), "CASE-3", "ACC-3", 50, 50, 10, 3, Guid.NewGuid(), now);
        item.Assign(assignedId, null, now);
        item.ApplyImportedDeskFields(null, "Habiba Mohamed", "30-59", null, fileId);

        Assert.False(item.TryAssignImportedFileCollector(null, now));
        Assert.Equal(assignedId, item.AssignedCollectorId);
        Assert.Equal(fileId, item.FileCollectorUserId);
    }

    [Fact]
    public void AmbiguousOrMissingCollectorNamesKeepTheSheetTextAndDoNotInventALink()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new CollectionCase(Guid.NewGuid(), Guid.NewGuid(), "CASE-2", "ACC-2", 80, 80, 20, 5, Guid.NewGuid(), now);

        item.ApplyImportedDeskFields("محمد", "غير معروف", "30-59");

        Assert.Equal("محمد", item.PreviousCollectorName);
        Assert.Equal("غير معروف", item.FileCollectorName);
        Assert.Null(item.PreviousCollectorUserId);
        Assert.Null(item.FileCollectorUserId);
        Assert.Null(item.AssignedCollectorId);
    }
}
