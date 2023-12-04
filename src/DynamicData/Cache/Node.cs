// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Node describing the relationship between and item and it's ancestors and descendent.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public class Node<TObject, TKey> : IDisposable, IEquatable<Node<TObject, TKey>>
    where TObject : class
    where TKey : notnull
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly SourceCache<Node<TObject, TKey>, TKey> _children = new(n => n.Key);

    private readonly CompositeDisposable _cleanUp;

    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Node{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="key">The key.</param>
    public Node(TObject item, TKey key)
        : this(item, key, default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Node{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="key">The key.</param>
    /// <param name="parent">The parent.</param>
    public Node(TObject item, TKey key, in Optional<Node<TObject, TKey>> parent)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Key = key;
        Parent = parent;
        Children = _children.AsObservableCache();
        _cleanUp = new(Children, _children);
    }

    /// <summary>
    /// Gets the child nodes.
    /// </summary>
    public IObservableCache<Node<TObject, TKey>, TKey> Children { get; }

    /// <summary>
    /// Gets the depth i.e. how many degrees of separation from the parent.
    ///  </summary>
    public int Depth
    {
        get
        {
            var i = 0;
            var parent = Parent;
            while (parent.HasValue)
            {
                i++;
                parent = parent.Value.Parent;
            }

            return i;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is root.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is root node; otherwise, <c>false</c>.
    /// </value>
    public bool IsRoot => !Parent.HasValue;

    /// <summary>
    /// Gets the item.
    /// </summary>
    public TObject Item { get; }

    /// <summary>
    /// Gets the key.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the parent if it has one.
    /// </summary>
    public Optional<Node<TObject, TKey>> Parent { get; internal set; }

    /// <summary>
    ///  Determines whether the specified objects are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>If the two values are equal.</returns>
    public static bool operator ==(Node<TObject, TKey>? left, Node<TObject, TKey>? right) => Equals(left, right);

    /// <summary>
    ///  Determines whether the specified objects are not equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>If the two values are not equal.</returns>
    public static bool operator !=(Node<TObject, TKey> left, Node<TObject, TKey> right) => !Equals(left, right);

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    /// <filterpriority>2.</filterpriority>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
    /// <param name="other">The object to compare with the current object. </param>
    /// <filterpriority>2.</filterpriority>
    public bool Equals(Node<TObject, TKey>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TKey>.Default.Equals(Key, other.Key);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
    /// <param name="obj">The object to compare with the current object. </param>
    /// <filterpriority>2.</filterpriority>
    public override bool Equals(object? obj)
    {
        if (obj is null)
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

        return Equals((Node<TObject, TKey>)obj);
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <returns>A hash code for the current object.</returns>
    /// <filterpriority>2.</filterpriority>
    public override int GetHashCode() => EqualityComparer<TKey>.Default.GetHashCode(Key);

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        var count = Children.Count == 0 ? string.Empty : $" ({Children.Count} children)";
        return $"{Item}{count}";
    }

    internal void Update(Action<ISourceUpdater<Node<TObject, TKey>, TKey>> updateAction) => _children.Edit(updateAction);

    /// <summary>
    /// Disposes any managed or unmanaged resources.
    /// </summary>
    /// <param name="isDisposing">If the dispose is being called by the Dispose method.</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (isDisposing)
        {
            _cleanUp.Dispose();
        }
    }
}
