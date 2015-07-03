using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData
{
    public class Node<TObject, TKey>: IDisposable, IEquatable<Node<TObject, TKey>> where TObject : class
    {
        
        private readonly ISourceCache<Node<TObject, TKey>,TKey> _children = new SourceCache<Node<TObject, TKey>, TKey>(n=>n.Key);
        private readonly IDisposable _cleanUp; 

        public Node(TObject item, TKey key)
            :this(item,key,null)
        {
        }

        public Node([NotNull] TObject item, TKey key, Optional<Node<TObject, TKey>> parent)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            Item = item;
            Key = key;
            Parent = parent;
            Children = _children.AsObservableCache();
            _cleanUp = new CompositeDisposable(Children, _children);
        }


        public TObject Item { get; }
        public TKey Key { get; }

        public Optional<Node<TObject, TKey>> Parent { get; internal set; }

        public IObservableCache<Node<TObject, TKey>, TKey> Children { get; }

        public bool IsRoot => !Parent.HasValue;

        #region Equality

        public bool Equals(Node<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Node<TObject, TKey>) obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TKey>.Default.GetHashCode(Key);
        }

        public static bool operator ==(Node<TObject, TKey> left, Node<TObject, TKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Node<TObject, TKey> left, Node<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        #endregion

        internal void Update(Action<ISourceUpdater<Node<TObject, TKey>, TKey>> updateAction)
        {
            _children.BatchUpdate(updateAction);
        }

        public override string ToString()
        {
            var count = Children.Count == 0 ? "" : $" ({Children.Count} children)";
            return $"{Item}{count}";
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}