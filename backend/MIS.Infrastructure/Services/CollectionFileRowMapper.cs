using System.Globalization;
using System.Text;
using System.Text.Json;
using MIS.Domain.Services;

namespace MIS.Infrastructure.Services;

internal sealed record CollectionFileRow(
    string Account,
    string? CardNumber,
    string? CustomerCode,
    string? NameArabic,
    string? NameEnglish,
    string? NationalId,
    string? Mobile1,
    string? Mobile2,
    string? Mobile3,
    string? Contract,
    string? Product,
    decimal? Outstanding,
    decimal? Overdue,
    int? DaysPastDue,
    string? BucketText,
    string? StatusText,
    string? Stage,
    string? Feedback,
    string? Region,
    string? Area,
    string? City,
    string? Address1,
    string? Address2,
    string? Employer,
    string? JobTitle,
    decimal? CreditLimit,
    decimal? PurchaseLimit,
    DateOnly? ActivationDate,
    decimal? LastPaymentAmount,
    DateOnly? LastPaymentDate,
    DateOnly? LastTransactionDate,
    decimal? LastTransactionAmount,
    string? PreviousCollector,
    string? FileCollector,
    string? Action,
    string? PtpDate,
    string? PtpAmount,
    string? Payment,
    string? Update,
    string? Keep);

internal static class CollectionFileRowMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DateFormats =
    {
        "M/d/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MMM-yy", "d-MMM-yy", "dd-MMM-yyyy",
        "M/d/yyyy H:mm:ss", "yyyy-MM-dd H:mm:ss", "yyyy-MM-dd HH:mm:ss.fffffff"
    };

    public static CollectionFileRow Read(IReadOnlyDictionary<string, string> values)
    {
        var (arabic, english) = ResolveNames(Get(values, Columns.NameGeneric), Get(values, Columns.NameAr), Get(values, Columns.NameEn));
        var national = DigitsOnly(Get(values, Columns.National));
        var account = First(values, Columns.Account);
        if (string.IsNullOrWhiteSpace(account)) account = First(values, Columns.Card);
        return new CollectionFileRow(
            account,
            NullIfEmpty(First(values, Columns.Card)),
            NullIfEmpty(Get(values, Columns.CustomerCode)),
            arabic, english,
            NullIfEmpty(national),
            NullIfEmpty(NormalizePhone(Get(values, Columns.Mobile1))),
            NullIfEmpty(NormalizePhone(Get(values, Columns.Mobile2))),
            NullIfEmpty(NormalizePhone(Get(values, Columns.Mobile3))),
            NullIfEmpty(Get(values, Columns.Contract)),
            NullIfEmpty(Get(values, Columns.Product)),
            ParseMoney(Get(values, Columns.Outstanding)),
            ParseMoney(Get(values, Columns.Overdue)),
            ParseInt(Get(values, Columns.Dpd)),
            NullIfEmpty(Get(values, Columns.Bucket)),
            NullIfEmpty(Get(values, Columns.Status)),
            NullIfEmpty(Get(values, Columns.Stage)),
            NullIfEmpty(Get(values, Columns.Feedback)),
            NullIfEmpty(Get(values, Columns.Region)),
            NullIfEmpty(Get(values, Columns.Area)),
            NullIfEmpty(Get(values, Columns.City)),
            NullIfEmpty(Get(values, Columns.Address1)),
            NullIfEmpty(Get(values, Columns.Address2)),
            NullIfEmpty(Get(values, Columns.Corporate)),
            NullIfEmpty(Get(values, Columns.JobTitle)),
            ParseMoney(Get(values, Columns.CreditLimit)),
            ParseMoney(Get(values, Columns.PurchaseLimit)),
            ParseDate(Get(values, Columns.ActivationDate)),
            ParseMoney(Get(values, Columns.LastPaymentAmount)),
            ParseDate(Get(values, Columns.LastPaymentDate)),
            ParseDate(Get(values, Columns.LastTxnDate)),
            ParseMoney(Get(values, Columns.LastTxnAmount)),
            NullIfEmpty(Get(values, Columns.OldCollector)),
            NullIfEmpty(Get(values, Columns.Collector)),
            NullIfEmpty(Get(values, Columns.Action)),
            NullIfEmpty(Get(values, Columns.PtpDate)),
            NullIfEmpty(Get(values, Columns.PtpAmount)),
            NullIfEmpty(Get(values, Columns.Payment)),
            NullIfEmpty(Get(values, Columns.Update)),
            NullIfEmpty(Get(values, Columns.Keep)));
    }

    public static CollectionFileRow Read(ParsedCollectionRow row) => Read(row.Values);

    public static string Serialize(IReadOnlyDictionary<string, string> values) => JsonSerializer.Serialize(values, JsonOptions);

    public const string AddressJoinMarker = " — ";

    public static (string? Primary, string? Secondary) SeparateAddresses(string? primary, string? secondary)
    {
        var first = NullIfEmpty(primary);
        var second = NullIfEmpty(secondary);
        if (first is null) return (null, second);
        if (second is not null)
        {
            var suffix = AddressJoinMarker + second;
            if (first.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return (NullIfEmpty(first[..^suffix.Length]), second);
            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
                return (first, null);
        }
        var index = first.IndexOf(AddressJoinMarker, StringComparison.Ordinal);
        if (index >= 0)
        {
            var left = NullIfEmpty(first[..index]);
            var right = NullIfEmpty(first[(index + AddressJoinMarker.Length)..]);
            return (left, second ?? right);
        }
        return (first, second);
    }

    public static bool HasArabic(string? value) => !string.IsNullOrEmpty(value) && value.Any(c => c is >= '\u0600' and <= '\u06FF');

    public static DateTimeOffset? ToTimestamp(DateOnly? date) =>
        date.HasValue ? new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

    public static string SnapshotValue(IReadOnlyDictionary<string, string> values, params string[] aliases) => Get(values, aliases);

    public static Dictionary<string, string>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var key = CollectionImportParser.NormalizeHeader(property.Name);
                if (key.Length == 0) continue;
                values[key] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString()?.Trim() ?? "",
                    JsonValueKind.Number => property.Value.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "",
                    _ => property.Value.ToString()
                };
            }
            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string NormalizePhone(string value)
    {
        var digits = DigitsOnly(value);
        return digits.StartsWith("20", StringComparison.Ordinal) && digits.Length == 12 ? "0" + digits[2..] : digits;
    }

    public static string DigitsOnly(string value)
    {
        const string arabic = "٠١٢٣٤٥٦٧٨٩"; const string eastern = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var index = arabic.IndexOf(c);
            if (index < 0) index = eastern.IndexOf(c);
            builder.Append(index >= 0 ? (char)('0' + index) : char.IsDigit(c) ? c : default);
        }
        return new string(builder.ToString().Where(char.IsDigit).ToArray());
    }

    public static class Columns
    {
        public static readonly string[] Account = { "case", "casenumber", "accountreference", "accountnumber", "رقمالحالة", "رقمالحساب", "مرجعالحساب" };
        public static readonly string[] Card = { "cardnumber", "card", "رقمالكارت", "رقمالبطاقة" };
        public static readonly string[] CustomerCode = { "customercode", "customerid", "كودالعميل" };
        public static readonly string[] NameGeneric = { "customername", "name", "اسمالعميل", "الاسم" };
        public static readonly string[] NameAr = { "namearabic", "customernamearabic", "اسمالعميلبالعربية" };
        public static readonly string[] NameEn = { "nameenglish", "customernameenglish" };
        public static readonly string[] National = { "nid", "nationalid", "الرقمالقومي", "الرقمالمدني" };
        public static readonly string[] Mobile1 = { "mobile1", "phones", "phone", "mobile", "primaryphone", "phonenumber", "رقمالهاتف", "الموبايل", "موبايل1", "تليفون1", "تليفون" };
        public static readonly string[] Mobile2 = { "mobile2", "alternatephone", "موبايل2", "تليفون2" };
        public static readonly string[] Mobile3 = { "mobile3", "tertiaryphone", "موبايل3", "تليفون3" };
        public static readonly string[] Contract = { "contractreference", "contractnumber", "رقمالعقد" };
        public static readonly string[] Product = { "producttype", "loantype", "نوعالمنتج" };
        public static readonly string[] Outstanding = { "currentbalance", "outstandingbalance", "outstanding", "الرصيدالقائم", "المديونية", "الرصيدالحالي" };
        public static readonly string[] Overdue = { "totaldues", "totaldue", "overduebalance", "overdue", "isdu", "isdue", "المتأخر", "الرصيدالمتأخر", "اجماليالمستحق", "إجماليالمستحق" };
        public static readonly string[] Dpd = { "dayspastdue", "dpd", "أيامالتأخر", "ايامالتأخر" };
        public static readonly string[] Bucket = { "currentbkt", "upbkt", "bkt", "bucket", "currentbucket", "الشريحة", "شريحةالتأخر" };
        public static readonly string[] Status = { "statue", "status", "الحالة", "حالة" };
        public static readonly string[] Feedback = { "feedback", "الملاحظات", "ملاحظات", "التغذيةالراجعة" };
        public static readonly string[] Region = { "region", "governorate", "المحافظة", "المنطقة" };
        public static readonly string[] Area = { "area", "الحي" };
        public static readonly string[] Address1 = { "address1", "address", "العنوان", "العنوان1" };
        public static readonly string[] Address2 = { "address2", "العنوان2" };
        public static readonly string[] City = { "city", "المدينة" };
        public static readonly string[] Corporate = { "corptarename", "corporatename", "corporate", "employer", "companyname", "جهةالعمل", "الشركة", "اسمالشركة" };
        public static readonly string[] JobTitle = { "jobtitle", "المسمىالوظيفي", "الوظيفة" };
        public static readonly string[] Stage = { "stage", "المرحلة" };
        public static readonly string[] CreditLimit = { "creditlimit", "الحدالائتماني", "حدالائتمان" };
        public static readonly string[] PurchaseLimit = { "purchaseavailablelimit", "availablelimit", "المتاحللشراء", "حدالشراءالمتاح" };
        public static readonly string[] ActivationDate = { "activationdate", "تاريخالتفعيل" };
        public static readonly string[] LastPaymentDate = { "lastpaymentdate", "تاريخآخرسداد", "تاريخاخرسداد" };
        public static readonly string[] LastPaymentAmount = { "lastpaymentamount", "مبلغآخرسداد", "مبلغاخرسداد", "قيمةآخرسداد" };
        public static readonly string[] LastTxnDate = { "lasttransactiondate", "تاريخآخرمعاملة", "تاريخاخرمعاملة" };
        public static readonly string[] LastTxnAmount = { "lasttransactionamount", "مبلغآخرمعاملة", "مبلغاخرمعاملة" };
        public static readonly string[] OldCollector = { "oldcoll", "oldcollector", "previouscollector", "المحصلالسابق" };
        public static readonly string[] Collector = { "collector", "coll", "المحصل" };
        public static readonly string[] Action = { "action", "الاجراء", "الإجراء" };
        public static readonly string[] PtpDate = { "dateptp", "ptpdate", "تاريخالوعد" };
        public static readonly string[] PtpAmount = { "amount", "ptpamount", "مبلغالوعد" };
        public static readonly string[] Payment = { "paymant", "payment", "المدفوع", "السداد" };
        public static readonly string[] Update = { "update", "التحديث" };
        public static readonly string[] Keep = { "keep", "truefalse", "truefulse", "trufalse" };
    }

    private static string First(IReadOnlyDictionary<string, string> values, string[] aliases) => Get(values, aliases);

    private static string Get(IReadOnlyDictionary<string, string> values, params string[] aliases)
    {
        foreach (var alias in aliases)
            if (values.TryGetValue(CollectionImportParser.NormalizeHeader(alias), out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        return string.Empty;
    }

    private static (string? Arabic, string? English) ResolveNames(string generic, string arabic, string english)
    {
        if (!string.IsNullOrWhiteSpace(arabic) || !string.IsNullOrWhiteSpace(english))
            return (NullIfEmpty(arabic), NullIfEmpty(english));
        var value = generic.Trim();
        if (value.Length == 0) return (null, null);
        return HasArabic(value) ? (value, null) : (null, value);
    }

    private static decimal? ParseMoney(string value)
    {
        var text = DigitsWithSeparators(value);
        return text.Length > 0 && decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static int? ParseInt(string value)
    {
        var digits = DigitsOnly(value);
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static DateOnly? ParseDate(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return null;
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)) return DateOnly.FromDateTime(stamp);
        return null;
    }

    private static string DigitsWithSeparators(string value)
    {
        const string arabic = "٠١٢٣٤٥٦٧٨٩"; const string eastern = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var index = arabic.IndexOf(c);
            if (index < 0) index = eastern.IndexOf(c);
            if (index >= 0) builder.Append((char)('0' + index));
            else if (char.IsDigit(c) || c is '.' or '-') builder.Append(c);
        }
        return builder.ToString();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
