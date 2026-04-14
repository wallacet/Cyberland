namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Declares that this system must run <strong>before</strong> the system registered under <see cref="TargetId"/>.
/// Multiple attributes may be applied. Resolved at registration time by <see cref="SystemScheduler"/> (see <see cref="RunAfterAttribute"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RunBeforeAttribute : Attribute
{
    /// <summary>Logical id of another registered system (e.g. <c>cyberland.engine/sprite-render</c>).</summary>
    public string TargetId { get; }

    /// <param name="targetId">Non-empty scheduler logical id to precede.</param>
    public RunBeforeAttribute(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target id must be non-empty.", nameof(targetId));
        TargetId = targetId;
    }
}

/// <summary>
/// Declares that this system must run <strong>after</strong> the system registered under <see cref="TargetId"/>.
/// Multiple attributes may be applied. Edges are merged with <see cref="RunBeforeAttribute"/> and resolved by <see cref="SystemScheduler"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RunAfterAttribute : Attribute
{
    /// <summary>Logical id of another registered system that must run earlier.</summary>
    public string TargetId { get; }

    /// <param name="targetId">Non-empty scheduler logical id to follow.</param>
    public RunAfterAttribute(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target id must be non-empty.", nameof(targetId));
        TargetId = targetId;
    }
}
