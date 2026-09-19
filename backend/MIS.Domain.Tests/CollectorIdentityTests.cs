using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectorIdentityTests
{
    [Fact]
    public void UniqueArabicNameMatchesOnceAndIgnoresAlefVariants()
    {
        var islam = Candidate(Guid.NewGuid(), "اسلام محمد");
        var directory = new[] { islam, Candidate(Guid.NewGuid(), "مروان علي") };

        Assert.Equal(islam.UserId, CollectorIdentity.UniqueMatch(directory, "إسلام محمد"));
        Assert.Equal(islam.UserId, CollectorIdentity.UniqueMatch(directory, "اسلام-محمد"));
    }

    [Fact]
    public void AmbiguousSharedNameIsLeftUnmatched()
    {
        var directory = new[]
        {
            Candidate(Guid.NewGuid(), "محمد أحمد"),
            Candidate(Guid.NewGuid(), "محمد احمد")
        };

        var match = CollectorIdentity.Match(directory, "محمد أحمد");
        Assert.Equal(CollectorMatchKind.Ambiguous, match.Kind);
        Assert.Null(CollectorIdentity.UniqueMatch(directory, "محمد أحمد"));
    }

    [Fact]
    public void EmployeeNumberAndUsernameWinOnlyWhenUnique()
    {
        var first = new CollectorIdentityCandidate(Guid.NewGuid(), "Islam", "islam", "islam@mis.local", "C-10", "اسلام", "اسلام", "Islam");
        var second = new CollectorIdentityCandidate(Guid.NewGuid(), "Other", "other", "other@mis.local", "C-11", "آخر", "آخر", "Other");
        var directory = new[] { first, second };

        Assert.Equal(first.UserId, CollectorIdentity.UniqueMatch(directory, "C-10"));
        Assert.Equal(first.UserId, CollectorIdentity.UniqueMatch(directory, "islam"));
        Assert.Null(CollectorIdentity.UniqueMatch(directory, " "));
        Assert.Null(CollectorIdentity.UniqueMatch(directory, "unknown"));
    }

    [Fact]
    public void UniqueNameOverlapMatchesALongerSystemName()
    {
        var habiba = Candidate(Guid.NewGuid(), "Habiba Mohamed Ali");
        var directory = new[] { habiba, Candidate(Guid.NewGuid(), "Mariam Hassan") };

        Assert.Equal(habiba.UserId, CollectorIdentity.UniqueMatch(directory, "Habiba Mohamed"));
        Assert.Null(CollectorIdentity.UniqueMatch(directory, "Habiba Mohamed Sayed Hassan"));
    }

    [Fact]
    public void BilingualFileNameMatchesEnglishSystemName()
    {
        var habiba = Candidate(Guid.NewGuid(), "Habiba Mohamed Ali");
        var directory = new[] { habiba, Candidate(Guid.NewGuid(), "Mariam Hassan") };

        Assert.Equal(habiba.UserId, CollectorIdentity.UniqueMatch(directory, "حبيبة محمد Habiba Mohamed"));
    }

    [Fact]
    public void BilingualFileNameMatchesArabicSystemName()
    {
        var habiba = new CollectorIdentityCandidate(Guid.NewGuid(), "Habiba Mohamed", "habiba", "habiba@mis.local", null, "حبيبة محمد", "حبيبة محمد", "Habiba Mohamed");
        var directory = new[] { habiba, Candidate(Guid.NewGuid(), "Mariam Hassan") };

        Assert.Equal(habiba.UserId, CollectorIdentity.UniqueMatch(directory, "حبيبة محمد Habiba Mohamed"));
        Assert.Equal(habiba.UserId, CollectorIdentity.UniqueMatch(directory, "حبيبة محمد"));
    }

    [Fact]
    public void ConflictingArabicAndEnglishPartsStayAmbiguous()
    {
        var arabic = new CollectorIdentityCandidate(Guid.NewGuid(), "حبيبة محمد", "arabic", "a@mis.local", null, "حبيبة محمد", "حبيبة محمد", null);
        var english = new CollectorIdentityCandidate(Guid.NewGuid(), "Habiba Mohamed", "english", "e@mis.local", null, "Habiba Mohamed", null, "Habiba Mohamed");
        var directory = new[] { arabic, english };

        var match = CollectorIdentity.Match(directory, "حبيبة محمد Habiba Mohamed");
        Assert.Equal(CollectorMatchKind.Ambiguous, match.Kind);
        Assert.Null(CollectorIdentity.UniqueMatch(directory, "حبيبة محمد Habiba Mohamed"));
    }

    private static CollectorIdentityCandidate Candidate(Guid id, string name) =>
        new(id, name, id.ToString("N")[..8], $"{id:N}@mis.local", null, name, name, name);
}
