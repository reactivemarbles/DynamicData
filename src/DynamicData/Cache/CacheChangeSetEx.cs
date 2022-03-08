// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache;

internal static class CacheChangeSetEx
{
    /// <summary>
    /// IChangeSet is flawed because it automatically means allocations when enumerating.
    /// This extension is a crazy hack to cast to the concrete change set which means we no longer allocate
    /// as  change set now inherits from List which has allocation free enumerations.
    ///
    /// IChangeSet will be removed in V7 and instead Change sets will be used directly
    ///
    /// In the mean time I am banking that no-one has implemented a custom change set - personally I think it is very unlikely.
    /// </summary>
    /// <param name="changeSet">The source change set.</param>
    public static ChangeSet<TObject, TKey> ToConcreteType<TObject, TKey>(this IChangeSet<TObject, TKey> changeSet)
        where TKey : notnull
        => (ChangeSet<TObject, TKey>)changeSet;
}
