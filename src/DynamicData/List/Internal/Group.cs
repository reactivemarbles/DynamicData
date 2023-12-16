// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

internal sealed class Group<TObject, TGroup>(TGroup groupKey) : IGroup<TObject, TGroup>, IDisposable, IEquatable<Group<TObject, TGroup>>
    where TObject : notnull
{
    public TGroup GroupKey { get; } = groupKey;

    public IObservableList<TObject> List => Source;

    private SourceList<TObject> Source { get; } = new();

    public static bool operator ==(Group<TObject, TGroup> left, Group<TObject, TGroup> right) => Equals(left, right);

    public static bool operator !=(Group<TObject, TGroup> left, Group<TObject, TGroup> right) => !Equals(left, right);

    public void Dispose() => Source.Dispose();

    public void Edit(Action<IList<TObject>> editAction) => Source.Edit(editAction);

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

    public override bool Equals(object? obj) => obj is Group<TObject, TGroup> value && Equals(value);

    public override int GetHashCode() => GroupKey is null ? 0 : EqualityComparer<TGroup>.Default.GetHashCode(GroupKey);

    public override string ToString() => $"Group of {GroupKey} ({List.Count} records)";
}
