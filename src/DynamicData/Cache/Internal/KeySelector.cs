// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the KeySelector class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="keySelector">The keySelector value.</param>
internal sealed class KeySelector<TObject, TKey>(Func<TObject, TKey> keySelector) : IKeySelector<TObject, TKey>
{
    /// <summary>
    /// The _keySelector field.
    /// </summary>
    private readonly Func<TObject, TKey> _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    /// <summary>
    /// Gets the Type value.
    /// </summary>
    [SuppressMessage("Design", "CA1822: Member can be static", Justification = "Backwards compatibilty")]
    public Type Type => typeof(TObject);

    /// <summary>
    /// Executes the GetKey operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public TKey GetKey(TObject item)
    {
        try
        {
            return _keySelector(item);
        }
        catch (Exception ex)
        {
            throw new KeySelectorException("Error returning key", ex);
        }
    }
}
