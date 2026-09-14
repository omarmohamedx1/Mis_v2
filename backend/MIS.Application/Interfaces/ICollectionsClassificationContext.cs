namespace MIS.Application.Interfaces;

/// <summary>
/// Ambient portfolio classification for the current request. Empty when the caller did not scope the
/// request to a classification, which keeps legacy and global collections pages unfiltered.
/// </summary>
public interface ICollectionsClassificationContext
{
    /// <summary>ACT, WO, or CORP. Null when the request is unscoped.</summary>
    string? Primary { get; }

    /// <summary>LOAN, VISA, or AUTO under ACT and WO; ACT or WO under CORP. Null when the request is unscoped.</summary>
    string? Sub { get; }

    bool HasValue { get; }

    /// <summary>Normalizes and stores the pair, rejecting unsupported combinations.</summary>
    void Set(string? primary, string? sub);

    /// <summary>Fails when only one half of the pair was supplied or the combination is unsupported.</summary>
    void RequireValid();
}
