using System;
using System.Collections.Generic;

namespace DynamicData.Binding
{
    /// <summary>
    /// Container holding sender and latest property value
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public sealed class PropertyValue<TObject, TValue> : IEquatable<PropertyValue<TObject, TValue>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyValue{TObject, TValue}"/> class.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="value">The value.</param>
        public PropertyValue(TObject sender, TValue value)
        {
            Sender = sender;
            Value = value;
        }

        internal PropertyValue(TObject sender)
        {
            Sender = sender;
            UnobtainableValue = true;
            Value = default(TValue);
        }

        /// <summary>
        /// The Sender
        /// </summary>
        public TObject Sender { get; }

        /// <summary>
        /// Lastest observed value
        /// </summary>
        public TValue Value { get; }
         
        /// <summary>
        /// Flag to indicated that the value was unobtainable when observing a deeply nested struct
        /// </summary>
        internal bool UnobtainableValue { get; }

        #region Equality

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(PropertyValue<TObject, TValue> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TObject>.Default.Equals(Sender, other.Sender) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);
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
            return obj is PropertyValue<TObject, TValue> && Equals((PropertyValue<TObject, TValue>)obj);
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
                return (EqualityComparer<TObject>.Default.GetHashCode(Sender) * 397) ^ EqualityComparer<TValue>.Default.GetHashCode(Value);
            }
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(PropertyValue<TObject, TValue> left, PropertyValue<TObject, TValue> right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(PropertyValue<TObject, TValue> left, PropertyValue<TObject, TValue> right)
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
            return $"{Sender} ({Value})";
        }
    }
}
