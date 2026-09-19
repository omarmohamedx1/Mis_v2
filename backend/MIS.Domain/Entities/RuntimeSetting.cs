namespace MIS.Domain.Entities;

public sealed class RuntimeSetting
{
    private RuntimeSetting() { }

    public RuntimeSetting(string key, string value, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Key = key.Trim();
        Value = value;
        UpdatedAt = updatedAt;
    }

    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Replace(string value, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        UpdatedAt = updatedAt;
    }
}
