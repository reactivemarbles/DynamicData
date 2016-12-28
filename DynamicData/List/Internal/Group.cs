using System;
using System.Collections.Generic;

namespace DynamicData.List.Internal
{
    internal class Group<TObject, TGroup> : IGroup<TObject, TGroup>, IDisposable, IEquatable<Group<TObject, TGroup>>
    {
        public TGroup GroupKey { get; }
        public IObservableList<TObject> List => Source;
        private ISourceList<TObject> Source { get; } = new SourceList<TObject>();

        public Group(TGroup groupKey)
        {
            GroupKey = groupKey;
        }

        public void Edit(Action<IList<TObject>> editAction)
        {
            Source.Edit(editAction);
        }

        #region Equality

        public bool Equals(Group<TObject, TGroup> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TGroup>.Default.Equals(GroupKey, other.GroupKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Group<TObject, TGroup>)obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TGroup>.Default.GetHashCode(GroupKey);
        }

        public static bool operator ==(Group<TObject, TGroup> left, Group<TObject, TGroup> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Group<TObject, TGroup> left, Group<TObject, TGroup> right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"Group of {GroupKey} ({List.Count} records)";
        }

        public void Dispose()
        {
            Source.Dispose();
        }
    }
}
