using System.Globalization;
using MIS.Application.Common;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ApiTextLocalizerTests
{
    [Fact]
    public void English_request_preserves_canonical_message_and_codes()
    {
        using var culture = new CultureScope("en");

        Assert.Equal("Employee was not found.", ApiTextLocalizer.Localize("Employee was not found.", true));
        Assert.Equal("Active", ApiTextLocalizer.LocalizeCode("Active"));
    }

    [Fact]
    public void Arabic_request_localizes_errors_and_validation_details()
    {
        using var culture = new CultureScope("ar");

        var response = ApiErrorResponse.Failure(
            "Validation failed.",
            ["The EmployeeId field is required.", "Attendance already exists for this employee and date."]);

        Assert.Equal("تعذر التحقق من صحة البيانات.", response.Message);
        Assert.Contains("حقل الموظف مطلوب.", response.Errors);
        Assert.Contains("يوجد بالفعل سجل حضور لهذا الموظف في هذا التاريخ.", response.Errors);
    }

    [Fact]
    public void Arabic_request_localizes_dynamic_mixed_language_audit_text()
    {
        using var culture = new CultureScope("ar");

        Assert.Equal("تم إنشاء الموظف أحمد علي.", ApiTextLocalizer.Localize("Created employee أحمد علي."));
        Assert.Equal("تم إنشاء الإدارة ضمن الفروع.", ApiTextLocalizer.Localize("Created الإدارة in branches."));
    }

    [Fact]
    public void Arabic_unknown_error_uses_safe_arabic_fallback()
    {
        using var culture = new CultureScope("ar");

        var localized = ApiTextLocalizer.Localize("A new unmapped technical failure.", true);

        Assert.Equal("تعذر إكمال الطلب. يُرجى مراجعة البيانات والمحاولة مرة أخرى.", localized);
    }

    [Theory]
    [InlineData("Claim was not found.", "لم يتم العثور على المطالبة. حدّث القائمة وحاول مرة أخرى.")]
    [InlineData("This claim is already cancelled.", "تم إلغاء هذه المطالبة بالفعل. حدّث القائمة لعرض حالتها الحالية.")]
    [InlineData("This claim cannot be cancelled.", "لا يمكن إلغاء المطالبة بحالتها الحالية.")]
    [InlineData("This claim has a posted receipt. Reverse the receipt through the authorized receipt workflow before cancelling the claim.", "لا يمكن إلغاء المطالبة لوجود تحصيل مرحّل لم يتم عكسه. يجب على مستخدم مخوّل عكس التحصيل من إجراء عكس التحصيل أولاً، ثم إعادة محاولة الإلغاء.")]
    public void Claim_cancellation_errors_preserve_actionable_arabic_reason(string message, string expected)
    {
        using var culture = new CultureScope("ar");
        Assert.Equal(expected, ApiErrorResponse.Failure(message).Message);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
