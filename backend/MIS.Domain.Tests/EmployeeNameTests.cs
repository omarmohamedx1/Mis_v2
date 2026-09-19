using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeNameTests
{
    [Theory]
    [InlineData("محمد أحمد علي")]
    [InlineData("د. منى عادل")]
    [InlineData(null)]
    [InlineData("")]
    public void Arabic_name_accepts_arabic_letters_and_empty(string? value) =>
        Assert.True(EmployeeName.IsArabicName(value));

    [Theory]
    [InlineData("Mohamed Ali")]
    [InlineData("Omar")]
    public void Arabic_name_rejects_english_letters(string value) =>
        Assert.False(EmployeeName.IsArabicName(value));

    [Theory]
    [InlineData("Mohamed Ahmed Ali")]
    [InlineData("Sara O'Brien")]
    [InlineData(null)]
    [InlineData("")]
    public void English_name_accepts_latin_letters_and_empty(string? value) =>
        Assert.True(EmployeeName.IsEnglishName(value));

    [Theory]
    [InlineData("محمد أحمد")]
    [InlineData("Omar محمد")]
    public void English_name_rejects_arabic_letters(string value) =>
        Assert.False(EmployeeName.IsEnglishName(value));

    [Fact]
    public void Require_methods_keep_valid_names_and_reject_mixed_script()
    {
        Assert.Equal("منى عادل", EmployeeName.RequireArabic(" منى عادل "));
        Assert.Equal("Mona Adel", EmployeeName.RequireEnglish(" Mona Adel "));
        Assert.Null(EmployeeName.RequireArabic(" "));
        Assert.Throws<ArgumentException>(() => EmployeeName.RequireArabic("Mona Adel"));
        Assert.Throws<ArgumentException>(() => EmployeeName.RequireEnglish("منى عادل"));
    }

    [Fact]
    public void SplitScripts_separates_bilingual_names()
    {
        var parts = EmployeeName.SplitScripts("حبيبة محمد Habiba Mohamed");
        Assert.Equal("حبيبة محمد", parts.Arabic);
        Assert.Equal("Habiba Mohamed", parts.English);
    }

    [Fact]
    public void FillMissing_splits_mixed_script_into_both_fields()
    {
        var names = EmployeeName.FillMissing(null, "حبيبة محمد Habiba Mohamed", null);
        Assert.Equal("حبيبة محمد", names.Arabic);
        Assert.Equal("Habiba Mohamed", names.English);
        Assert.Equal("حبيبة محمد Habiba Mohamed", names.Canonical);
    }
}
