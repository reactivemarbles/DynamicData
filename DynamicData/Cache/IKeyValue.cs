// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A keyed value
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IKeyValue<out TObject, out TKey> : IKey<TKey>
    {
        /// <summary>
        /// The value
        /// </summary>
        TObject Value { get; }
    }

    /// <summary>
    /// Represents the key of an object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IKey<out T>
    {
        /// <summary>
        /// The key 
        /// </summary>
        T Key { get; }
    }
}
