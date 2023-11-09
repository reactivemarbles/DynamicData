// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache;

internal static class CacheChangeSetEx
{
    /// <summary>
    /// IChangeSet is flawed because it automatically means allocations when enumerating.
    /// This extension is a crazy hack to cast to the concrete change set which means we no longer allocate
    /// as change set now inherits from List which has allocation free enumerations.
    ///
    /// IChangeSet will be removed in a future version and instead <see cref="ChangeSet{TObject, TKey}"/> will be used directly.
    ///
    /// In the mean time I am banking that no-one has implemented a custom change set - personally I think it is very unlikely.
    /// </summary>
    /// <typeparam name="TObject">ChangeSet Object Type.</typeparam>
    /// <typeparam name="TKey">ChangeSet Key Type.</typeparam>
    /// <param name="changeSet">ChangeSet to be converted.</param>
    /// <returns>Concrete Instance of the ChangeSet.</returns>
    /// <exception cref="NotSupportedException">A custom implementation was found.</exception>
    public static ChangeSet<TObject, TKey> ToConcreteType<TObject, TKey>(this IChangeSet<TObject, TKey> changeSet)
        where TObject : notnull
        where TKey : notnull =>
            changeSet as ChangeSet<TObject, TKey> ?? throw new NotSupportedException("Dynamic Data does not support a custom implementation of IChangeSet");
}
