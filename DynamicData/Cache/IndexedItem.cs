using System.Collections.Generic;

namespace DynamicData
{
    public sealed class IndexedItem<TObject, TKey> //: IIndexedItem<TObject, TKey>
    {
        #region Fields

        private  int _index;
        private readonly TKey _key;
        private readonly int _page;
        private readonly TObject _value;

        #endregion

        public IndexedItem(TObject value, TKey key, int index, int page = 1)
        {
            _index = index;
            _value = value;
            _page = page;
            _key = key;
        }

        #region Properties

        public int Index
        {
            get { return _index; }
            set {  _index=value; }
        }

        public TObject Value
        {
            get { return _value; }
        }


        public TKey Key
        {
            get { return _key; }
        }

        #endregion

        #region Equality

        public bool Equals(IndexedItem<TObject, TKey> other)
        {
            return EqualityComparer<TKey>.Default.Equals(_key, other._key) &&
                   EqualityComparer<TObject>.Default.Equals(_value, other._value) && _index == other._index &&
                   _page == other._page;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IndexedItem<TObject, TKey>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = EqualityComparer<TKey>.Default.GetHashCode(_key);
                hashCode = (hashCode*397) ^ EqualityComparer<TObject>.Default.GetHashCode(_value);
                hashCode = (hashCode*397) ^ _index;
                hashCode = (hashCode*397) ^ _page;
                return hashCode;
            }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("Value: {0}, Key: {1}, CurrentIndex: {2}, Page: {3}", _value, _key, _index, _page);
        }
    }
}