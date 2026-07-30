// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the Group class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TGroup">The type of the TGroup value.</typeparam>
/// <param name="groupKey">The groupKey value.</param>
internal sealed class Group<TObject, TGroup>(TGroup groupKey) : IGroup<TObject, TGroup>, IDisposable, IEquatable<Group<TObject, TGroup>>
    where TObject : notnull
{
    /// <summary>
    /// Gets the GroupKey value.
    /// </summary>
    public TGroup GroupKey { get; } = groupKey;

    /// <summary>
    /// Gets the List value.
    /// </summary>
    public IObservableList<TObject> List => Source;

    /// <summary>
    /// Gets the Source value.
    /// </summary>
    private SourceList<TObject> Source { get; } = new();

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator ==(Group<TObject, TGroup> left, Group<TObject, TGroup> right) => Equals(left, right);

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator !=(Group<TObject, TGroup> left, Group<TObject, TGroup> right) => !Equals(left, right);

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => Source.Dispose();

    /// <summary>
    /// Executes the Edit operation.
    /// </summary>
    /// <param name="editAction">The editAction value.</param>
    public void Edit(Action<IList<TObject>> editAction) => Source.Edit(editAction);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(Group<TObject, TGroup>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TGroup>.Default.Equals(GroupKey, other.GroupKey);
    }

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj) => obj is Group<TObject, TGroup> value && Equals(value);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode() => GroupKey is null ? 0 : EqualityComparer<TGroup>.Default.GetHashCode(GroupKey);

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"Group of {GroupKey} ({List.Count} records)";
}
