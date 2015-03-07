using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
    /// <summary>
    ///   Container to describe a single change to a cache
    /// </summary>
    public  struct Change<TObject, TKey> 
    {
        #region Fields

        private readonly TObject _current;
        private readonly TKey _key;
        private readonly Optional<TObject> _previous;
        private readonly ChangeReason _reason;
        private readonly int _currentIndex;
        private readonly int _previousIndex;

        public readonly static Change<TObject, TKey> Empty = new Change<TObject, TKey>();

        #endregion

        #region Construction


        /// <summary>
        /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        /// <param name="index">The index.</param>
        public Change(ChangeReason reason, TKey key, TObject current, int index=-1)
            : this(reason, key, current, Optional.None<TObject>(), index,-1)
        {

        }



        /// <summary>
        /// Construtor for ChangeReason.Move
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        /// <param name="currentIndex">The CurrentIndex.</param>
        /// <param name="previousIndex">CurrentIndex of the previous.</param>
        /// <exception cref="System.ArgumentException">
        /// CurrentIndex must be greater than or equal to zero
        /// or
        /// PreviousIndex must be greater than or equal to zero
        /// </exception>
        public Change( TKey key, TObject current, int currentIndex , int previousIndex)
            : this()
        {
            if (currentIndex<0)
                throw new ArgumentException("CurrentIndex must be greater than or equal to zero");
            
            if (previousIndex < 0)
                throw new ArgumentException("PreviousIndex must be greater than or equal to zero");
          
            _current = current;
            _previous = Optional.None<TObject>();
            _key = key;
            _reason = ChangeReason.Moved;
            _currentIndex = currentIndex;
            _previousIndex = previousIndex;

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        /// <param name="previous">The previous.</param>
        /// <param name="currentIndex">Value of the current.</param>
        /// <param name="previousIndex">Value of the previous.</param>
        /// <exception cref="System.ArgumentException">
        /// For ChangeReason.Add, a previous value cannot be specified
        /// or
        /// For ChangeReason.Change, must supply previous value
        /// </exception>
        public Change(ChangeReason reason, TKey key, TObject current, Optional<TObject> previous, int currentIndex=-1,int previousIndex=-1)
            :this()
        {
            _current = current;
            _previous = previous;
            _key = key;
            _reason = reason;
            _currentIndex = currentIndex;
            _previousIndex = previousIndex;

            if (reason == ChangeReason.Add && previous.HasValue)
            {
                throw new ArgumentException("For ChangeReason.Add, a previous value cannot be specified");
            }

            if (reason == ChangeReason.Update && !previous.HasValue)
            {
                throw new ArgumentException("For ChangeReason.Change, must supply previous value");
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// The unique key of the item which has changed
        /// </summary>
        public TKey Key
        {
            get { return _key; }
        }


        /// <summary>
        /// The  reason for the change
        /// </summary>
        public ChangeReason Reason
        {
            get { return _reason; }
        }


        /// <summary>
        /// The item which has changed
        /// </summary>
        public TObject Current
        {
            get { return _current; }
        }

        /// <summary>
        /// The current index
        /// </summary>
        public int CurrentIndex
        {
            get { return _currentIndex; }
        }

        /// <summary>
        /// The previous change.
        /// 
        /// This is only when Reason==ChangeReason.Update.
        /// </summary>
        public Optional<TObject> Previous
        {
            get { return _previous; }
        }

        /// <summary>
        /// The previous change.
        /// 
        /// This is only when Reason==ChangeReason.Update or ChangeReason.Move.
        /// </summary>
        public int PreviousIndex
        {
            get { return _previousIndex; }
        }




        #endregion

        #region Overrides

        public override string ToString()
        {
            return string.Format("{0}, Key: {1}, Current: {2}, Previous: {3}", Reason, Key, Current, Previous);
        }

        #endregion

        #region IEquatable<Change<T>> Members

        //public bool IsEmpty()
        //{
        //    return this.Equals(Empty);
        //}

        public bool Equals(Change<TObject, TKey> other)
        {
            return _reason.Equals(other._reason) && EqualityComparer<TKey>.Default.Equals(_key, other._key) &&
                   EqualityComparer<TObject>.Default.Equals(_current, other._current);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != GetType()) return false;
            return Equals((Change<TObject, TKey>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = _reason.GetHashCode();
                hashCode = (hashCode*397) ^ EqualityComparer<TKey>.Default.GetHashCode(_key);
                hashCode = (hashCode*397) ^ EqualityComparer<TObject>.Default.GetHashCode(_current);
                return hashCode;
            }
        }

        #endregion
    }
}