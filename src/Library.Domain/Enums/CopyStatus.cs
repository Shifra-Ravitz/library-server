namespace Library.Domain.Enums;

/// <summary>
/// Availability state of a single physical book copy.
/// </summary>
public enum CopyStatus
{
    Available = 0,
    Loaned = 1,

    /// <summary>
    /// A copy taken out of circulation.
    /// </summary>
    Retired = 2
}
