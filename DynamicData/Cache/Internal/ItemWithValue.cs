namespace DynamicData.Internal
{
    /// <summary>
    /// Container for the item  which sent an observable's subscribed to value
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public sealed class ItemWithValue<TObject, TValue>
    {
		/// <summary>
		/// Gets the item.
		/// </summary>
		public TObject Item { get; }

		/// <summary>
		/// Gets the value.
		/// </summary>
		public TValue Value { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ItemWithValue{TObject, TValue}"/> class.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="value">The value.</param>
		public ItemWithValue(TObject item, TValue value)
        {
            Item = item;
            Value = value;
        }


    }
}