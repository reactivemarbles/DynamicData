using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using DynamicData.Annotations;
using DynamicData.Kernel;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Node describing the relationship between and item and it's ancestors and decendents
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class Node<TObject, TKey> : IDisposable, IEquatable<Node<TObject, TKey>>
        where TObject : class
    {
        private readonly ISourceCache<Node<TObject, TKey>, TKey> _children = new SourceCache<Node<TObject, TKey>, TKey>(n => n.Key);
        private readonly IDisposable _cleanUp;

        /// <summary>
        /// Initializes a new instance of the <see cref="Node{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        public Node(TObject item, TKey key)
            : this(item, key, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Node{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        /// <param name="parent">The parent.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public Node([NotNull] TObject item, TKey key, Optional<Node<TObject, TKey>> parent)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            Item = item;
            Key = key;
            Parent = parent;
            Children = _children.AsObservableCache();
            _cleanUp = new CompositeDisposable(Children, _children);
        }

        /// <summary>
        /// The item
        /// </summary>
        public TObject Item { get; }

        /// <summary>
        /// The key
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the parent if it has one
        /// </summary>
        public Optional<Node<TObject, TKey>> Parent { get; internal set; }

        /// <summary>
        /// The child nodes
        /// </summary>
        public IObservableCache<Node<TObject, TKey>, TKey> Children { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is root.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is root node; otherwise, <c>false</c>.
        /// </value>
        public bool IsRoot => !Parent.HasValue;

        /// <summary>
        /// Gets the depth i.e. how many degrees of seperation from the parent
        ///  </summary>
        public int Depth
        {
            get
            {
                var i = 0;
                var parent = Parent;
                do
                {
                    if (!parent.HasValue)
                        break;
                    i++;
                    parent = parent.Value.Parent;
                } while (true);
                return i;
            }
        }

        internal void Update(Action<ISourceUpdater<Node<TObject, TKey>, TKey>> updateAction)
        {
            _children.Edit(updateAction);
        }

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
            return Equals((Node<TObject, TKey>)obj);
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

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var count = Children.Count == 0 ? "" : $" ({Children.Count} children)";
            return $"{Item}{count}";
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}
