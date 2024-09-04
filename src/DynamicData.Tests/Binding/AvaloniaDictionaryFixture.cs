using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using  DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Binding;

public class AvaloniaDictionaryFixture
{
    private readonly AvaloniaDictionary<string, Person> _collection;
    private readonly ChangeSetAggregator<Person> _results;

    public AvaloniaDictionaryFixture()
    {
        _collection = new AvaloniaDictionary<string, Person>();
        _results =  _collection.ToObservableChangeSet<AvaloniaDictionary<string, Person>, KeyValuePair<string, Person>>()
            .Transform(x=>x.Value)
            .AsAggregator();
    }

    [Fact]
    public void Add()
    {
        var person = new Person("Someone",10, "M");

        _collection.Add("Someone", person);

        _results.Messages.Count.Should().Be(2);
        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(person);
    }

    [Fact]
    public void Replace()
    {
        var person1 = new Person("Someone", 10, "M");
        var person2 = new Person("Someone", 11, "M");

        _collection.Add("Someone", person1);
        _collection["Someone"] = person2;

        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(person2);
    }


    [Fact]
    public void Remove()
    {
        var person = new Person("Someone", 10, "M");

        _collection.Add("Someone", person);
        _collection.Remove(person.Key);

        _results.Data.Count.Should().Be(0);
    }
}


public interface IAvaloniaDictionary<TKey, TValue>
    : IDictionary<TKey, TValue>,
        IAvaloniaReadOnlyDictionary<TKey, TValue>,
        IDictionary
    where TKey : notnull
{
}


public interface IAvaloniaReadOnlyDictionary<TKey, TValue>
    : IReadOnlyDictionary<TKey, TValue>,
        INotifyCollectionChanged,
        INotifyPropertyChanged
    where TKey : notnull
{
}


/*
  Copied from Avalionia because an issue was raised due to compatibility issues with ToObservableChangeSet().

There's not other way of testing it.

See https://github.com/AvaloniaUI/Avalonia/blob/d7c82a1a6f7eb95b2214f20a281fa0581fb7b792/src/Avalonia.Base/Collections/AvaloniaDictionary.cs#L17
 */
public class AvaloniaDictionary<TKey, TValue> : IAvaloniaDictionary<TKey, TValue> where TKey : notnull
{
    private Dictionary<TKey, TValue> _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaDictionary{TKey, TValue}"/> class.
    /// </summary>
    public AvaloniaDictionary()
    {
        _inner = new Dictionary<TKey, TValue>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaDictionary{TKey, TValue}"/> class.
    /// </summary>
    public AvaloniaDictionary(int capacity)
    {
        _inner = new Dictionary<TKey, TValue>(capacity);
    }

    /// <summary>
    /// Occurs when the collection changes.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Raised when a property on the collection changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public int Count => _inner.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public ICollection<TKey> Keys => _inner.Keys;

    /// <inheritdoc/>
    public ICollection<TValue> Values => _inner.Values;

    bool IDictionary.IsFixedSize => ((IDictionary)_inner).IsFixedSize;

    ICollection IDictionary.Keys => ((IDictionary)_inner).Keys;

    ICollection IDictionary.Values => ((IDictionary)_inner).Values;

    bool ICollection.IsSynchronized => ((IDictionary)_inner).IsSynchronized;

    object ICollection.SyncRoot => ((IDictionary)_inner).SyncRoot;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _inner.Keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _inner.Values;

    /// <summary>
    /// Gets or sets the named resource.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The resource, or null if not found.</returns>
    public TValue this[TKey key]
    {
        get
        {
            return _inner[key];
        }

        set
        {
            bool replace = _inner.TryGetValue(key, out var old);
            _inner[key] = value;

            if (replace)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{CommonPropertyNames.IndexerName}[{key}]"));

                if (CollectionChanged != null)
                {
                    var e = new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace,
                        new KeyValuePair<TKey, TValue>(key, value),
                        new KeyValuePair<TKey, TValue>(key, old!));
                    CollectionChanged(this, e);
                }
            }
            else
            {
                NotifyAdd(key, value);
            }
        }
    }

    object? IDictionary.this[object key] { get => ((IDictionary)_inner)[key]; set => ((IDictionary)_inner)[key] = value; }

    /// <inheritdoc/>
    public void Add(TKey key, TValue value)
    {
        _inner.Add(key, value);
        NotifyAdd(key, value);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var old = _inner;

        _inner = new Dictionary<TKey, TValue>();

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(CommonPropertyNames.IndexerName));


        if (CollectionChanged != null)
        {
            var e = new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                old.ToArray(),
                -1);
            CollectionChanged(this, e);
        }
    }

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ((IDictionary<TKey, TValue>)_inner).CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc/>
    public bool Remove(TKey key)
    {
        if (_inner.TryGetValue(key, out var value))
        {
            _inner.Remove(key);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{CommonPropertyNames.IndexerName}[{key}]"));

            if (CollectionChanged != null)
            {
                var e = new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    new[] { new KeyValuePair<TKey, TValue>(key, value) },
                    -1);
                CollectionChanged(this, e);
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _inner.TryGetValue(key, out value);
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    /// <inheritdoc/>
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_inner).CopyTo(array, index);

    /// <inheritdoc/>
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        return _inner.Contains(item);
    }

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        return Remove(item.Key);
    }

    /// <inheritdoc/>
    void IDictionary.Add(object key, object? value) => Add((TKey)key, (TValue)value!);

    /// <inheritdoc/>
    bool IDictionary.Contains(object key) => ((IDictionary)_inner).Contains(key);

    /// <inheritdoc/>
    IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)_inner).GetEnumerator();

    /// <inheritdoc/>
    void IDictionary.Remove(object key) => Remove((TKey)key);

    private void NotifyAdd(TKey key, TValue value)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{CommonPropertyNames.IndexerName}[{key}]"));


        if (CollectionChanged != null)
        {
            var e = new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                new[] { new KeyValuePair<TKey, TValue>(key, value) },
                -1);
            CollectionChanged(this, e);
        }
    }
}
internal static class CommonPropertyNames
{
    public const string IndexerName = "Item";
}
