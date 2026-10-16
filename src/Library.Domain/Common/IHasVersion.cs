namespace Library.Domain.Common;

/// <summary>
/// Marks an entity that is protected by optimistic concurrency.
/// The <see cref="Version"/> token is regenerated on every update so that
/// concurrent modifications can be detected.
/// </summary>
public interface IHasVersion
{
    /// <summary>
    /// Concurrency token; replaced with a new value whenever the entity changes.
    /// </summary>
    Guid Version { get; set; }
}
