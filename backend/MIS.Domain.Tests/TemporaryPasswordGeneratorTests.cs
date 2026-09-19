using MIS.Application.Common;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void GeneratedPasswordIsMemorableAndMeetsAdminStrengthRules()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 40; i++)
        {
            var password = TemporaryPasswordGenerator.Create();
            Assert.True(seen.Add(password));
            Assert.True(password.Length >= 12);
            Assert.Contains('-', password);
            Assert.EndsWith("!", password);
            Assert.Contains(password, static c => char.IsUpper(c));
            Assert.Contains(password, static c => char.IsLower(c));
            Assert.Contains(password, static c => char.IsDigit(c));
            Assert.Contains(password, static c => !char.IsLetterOrDigit(c));
        }
    }
}
