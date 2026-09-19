using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionFileRowMapperTests
{
    [Fact]
    public void Maps_premium_card_portfolio_headers()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Set(string header, string value) => values[CollectionImportParser.NormalizeHeader(header)] = value;

        Set("Case", "PREMIUM_CARD-ACT-LOAN-13890");
        Set("Card Number", "1234567890123456");
        Set("Statue", "Active");
        Set("NID", "12345678901234");
        Set("old coll", "Ahmed");
        Set("Collector", "Ola");
        Set("Customer Name", "Mohamed Reda Abd Elmoniem Ahmed");
        Set("Current BKT", "BKT More");
        Set("up BKT", "BKT More");
        Set("Current Balance", "4370.49");
        Set("Total Dues", "1200");
        Set("is du", "1");
        Set("Phones", "01118934445");
        Set("Mobile 1", "01000000000");
        Set("Region", "Cairo");
        Set("Area", "Nasr City");
        Set("Address 1", "Street 1");
        Set("Address 2", "Street 2");
        Set("Corptare Name", "ACME");
        Set("Job Title", "Officer");
        Set("City", "Cairo");
        Set("Stage", "Legal");
        Set("action", "PTP");
        Set("date ptp", "2026-09-20");
        Set("amount", "500");
        Set("Paymant", "100");
        Set("update", "called");
        Set("true \\ fulse", "true");
        Set("keep", "keep");
        Set("feedback", "follow up");

        var row = CollectionFileRowMapper.Read(values);

        Assert.Equal("PREMIUM_CARD-ACT-LOAN-13890", row.Account);
        Assert.Equal("1234567890123456", row.CardNumber);
        Assert.Equal("12345678901234", row.NationalId);
        Assert.Equal("Mohamed Reda Abd Elmoniem Ahmed", row.NameEnglish);
        Assert.Equal("01000000000", row.Mobile1);
        Assert.Equal(4370.49m, row.Outstanding);
        Assert.Equal(1200m, row.Overdue);
        Assert.Equal("BKT More", row.BucketText);
        Assert.Equal("Active", row.StatusText);
        Assert.Equal("Ahmed", row.PreviousCollector);
        Assert.Equal("Ola", row.FileCollector);
        Assert.Equal("PTP", row.Action);
        Assert.Equal("500", row.PtpAmount);
        Assert.Equal("100", row.Payment);
        Assert.Equal("keep", row.Keep);
        Assert.Equal("follow up", row.Feedback);
        Assert.Equal("ACME", row.Employer);
        Assert.Equal("Cairo", row.City);
    }

    [Fact]
    public void Phones_column_fills_mobile_when_mobile1_is_empty()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CollectionImportParser.NormalizeHeader("Case")] = "CASE-1",
            [CollectionImportParser.NormalizeHeader("Phones")] = "01118934445",
            [CollectionImportParser.NormalizeHeader("Current Balance")] = "10",
        };

        var row = CollectionFileRowMapper.Read(values);
        Assert.Equal("01118934445", row.Mobile1);
    }

    [Fact]
    public void Deserialize_accepts_mixed_json_values()
    {
        var values = CollectionFileRowMapper.Deserialize("""{"case":"A-1","keep":true,"amount":500,"note":null}""");
        Assert.NotNull(values);
        Assert.Equal("A-1", values!["case"]);
        Assert.Equal("true", values["keep"]);
        Assert.Equal("500", values["amount"]);
        Assert.Equal("", values["note"]);
    }
}
