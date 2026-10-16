using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using MIS.Application.DTOs.Collections;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionAmountValidationTests
{
    [Theory]
    [InlineData("ar")]
    [InlineData("ar-EG")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void PaymentAndPromiseAmounts_ValidateWithoutCultureDependentExceptions(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName, useUserOverride: false);
            foreach (var (type, name) in new[]
            {
                (typeof(SubmitPaymentRequest), "Amount"),
                (typeof(CreatePromiseRequest), "PromisedAmount"),
                (typeof(CreateBankPtpRequest), "PromiseAmount")
            })
            {
                // ASP.NET validates these records using constructor parameter metadata.
                // Validator.TryValidateObject only reads properties and misses these ranges.
                var range = type.GetConstructors().Single().GetParameters().Single(parameter => parameter.Name == name)
                    .GetCustomAttribute<RangeAttribute>()!;
                Assert.True(range.IsValid(400m));
                Assert.True(range.IsValid(0.01m));
                Assert.True(range.IsValid(9999999999999999m));
                Assert.False(range.IsValid(0m));
                Assert.False(range.IsValid(0.009m));
                Assert.False(range.IsValid(10000000000000000m));
                Assert.True(range.IsValid("400.25"));
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    public void AllCollectionsDecimalRangeLimits_UseInvariantConversion()
    {
        var ranges = typeof(SubmitPaymentRequest).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SubmitPaymentRequest).Namespace)
            .SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .SelectMany(parameter => parameter.GetCustomAttributes<RangeAttribute>())
            .Where(range => range.OperandType == typeof(decimal))
            .ToArray();
        Assert.NotEmpty(ranges);
        Assert.All(ranges, range =>
        {
            Assert.True(range.ParseLimitsInInvariantCulture);
            Assert.True(range.ConvertValueInInvariantCulture);
        });
    }
}
