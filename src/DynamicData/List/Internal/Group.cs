// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.List.Internal
{
    internal class Group<TObject, TGroup> : IGroup<TObject, TGroup>, IDisposable, IEquatable<Group<TObject, TGroup>>
    {
        private ISourceList<TObject> Source { get; } = new SourceList<TObject>();

        public TGroup GroupKey { get; }
        public IObservableList<TObject> List => Source;
        public Group(TGroup groupKey) => GroupKey = groupKey;

        public void Edit(Action<IList<TObject>> editAction) => Source.Edit(editAction);

        #region Equality

        public bool Equals(Group<TObject, TGroup> other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<TGroup>.Default.Equals(GroupKey, other.GroupKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

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

        public override string ToString() => $"Group of {GroupKey} ({List.Count} records)";

        public void Dispose() => Source.Dispose();
    }
}
