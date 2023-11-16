// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Cache.Internal;

internal sealed class KeySelector<TObject, TKey>(Func<TObject, TKey> keySelector) : IKeySelector<TObject, TKey>
{
    private readonly Func<TObject, TKey> _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    [SuppressMessage("Design", "CA1822: Member can be static", Justification = "Backwards compatibilty")]
    public Type Type => typeof(TObject);

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
