using MIS.Domain.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeePersonnelFileRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 42)]
    [InlineData(4, 57)]
    [InlineData(6, 85)]
    [InlineData(7, 100)]
    public void Male_employee_requires_seven_documents(int uploaded, int percentage)
    {
        Assert.True(EmployeePersonnelFileRules.IsMilitaryDocumentRequired("Male"));
        Assert.Equal(7, EmployeePersonnelFileRules.RequiredDocumentCount("Male"));
        Assert.Equal(percentage, EmployeePersonnelFileRules.CompletionPercentage(uploaded, "Male"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 50)]
    [InlineData(4, 66)]
    [InlineData(5, 83)]
    [InlineData(6, 100)]
    public void Female_employee_requires_six_documents(int uploaded, int percentage)
    {
        Assert.False(EmployeePersonnelFileRules.IsMilitaryDocumentRequired("Female"));
        Assert.Equal(6, EmployeePersonnelFileRules.RequiredDocumentCount("Female"));
        Assert.Equal(percentage, EmployeePersonnelFileRules.CompletionPercentage(uploaded, "Female"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Female", false)]
    [InlineData(" male ", true)]
    public void Military_document_is_required_only_for_male_employees(string? gender, bool expected)
    {
        Assert.Equal(expected, EmployeePersonnelFileRules.IsMilitaryDocumentRequired(gender));
    }

    [Theory]
    [InlineData("Male", -1)]
    [InlineData("Male", 8)]
    [InlineData("Female", -1)]
    [InlineData("Female", 7)]
    public void Completion_rejects_counts_outside_the_checklist(string gender, int uploaded)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EmployeePersonnelFileRules.CompletionPercentage(uploaded, gender));
    }
}
