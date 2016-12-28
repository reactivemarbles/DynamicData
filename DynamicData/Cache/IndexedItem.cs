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

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(IndexedItem<TObject, TKey> other)
        {
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) &&
                   EqualityComparer<TObject>.Default.Equals(Value, other.Value) && Index == other.Index;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IndexedItem<TObject, TKey>)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
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

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"Value: {Value}, Key: {Key}, CurrentIndex: {Index}";
        }
    }
}
