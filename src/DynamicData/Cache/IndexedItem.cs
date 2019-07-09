using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An item with it's index
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class IndexedItem<TObject, TKey> //: IIndexedItem<TObject, TKey>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IndexedItem{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        public IndexedItem(TObject value, TKey key, int index)
        {
            Index = index;
            Value = value;
            Key = key;
        }

        #region Properties

        /// <summary>
        /// Gets the index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public TObject Value { get; }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public TKey Key { get; }

        #endregion

        #region Equality


        private bool Equals(IndexedItem<TObject, TKey> other)
        {
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) &&
                   EqualityComparer<TObject>.Default.Equals(Value, other.Value) && Index == other.Index;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IndexedItem<TObject, TKey>)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = EqualityComparer<TKey>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ EqualityComparer<TObject>.Default.GetHashCode(Value);
                hashCode = (hashCode * 397) ^ Index;
                return hashCode;
            }
        }

        #endregion

        /// <inheritdoc />
        public override string ToString() => $"Value: {Value}, Key: {Key}, CurrentIndex: {Index}";
    }
}
