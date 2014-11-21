using System;
using System.Collections.Generic;
using System.Threading;

namespace DynamicData.Diagnostics
{
    /// <summary>
    /// Container for an object with the worker thread 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ThreadHolder<T> : IEquatable<ThreadHolder<T>>
    {
        private readonly T _value;
        private readonly Thread _threadId;


        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadHolder{T}" /> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="thread">The thread.</param>
        public ThreadHolder(T value, Thread thread)
            : this()
        {
            _value = value;
            _threadId = thread;
        }



        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public T Value
        {
            get { return _value; }
        }

        /// <summary>
        /// Gets the thread.
        /// </summary>
        /// <value>
        /// The thread.
        /// </value>
        public Thread Thread
        {
            get { return _threadId; }
        }

        public override string ToString()
        {
            return string.Format("Thread {0}. Value = {1}", this.Thread, this.Value);
        }
        #region Equality Members

        public bool Equals(ThreadHolder<T> other)
        {
            return EqualityComparer<T>.Default.Equals(_value, other._value) && Equals(_threadId, other._threadId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ThreadHolder<T> && Equals((ThreadHolder<T>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(_value)*397) ^ (_threadId != null ? _threadId.GetHashCode() : 0);
            }
        }

        #endregion
    }

}
