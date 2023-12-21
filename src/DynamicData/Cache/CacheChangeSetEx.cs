// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache;

internal static class CacheChangeSetEx
{
    /// <summary>
    /// <para>
    /// IChangeSet is flawed because it automatically means allocations when enumerating.
    /// This extension is a crazy hack to cast to the concrete change set which means we no longer allocate
    /// as change set now inherits from List which has allocation free enumerations.
    /// </para>
    /// <para>IChangeSet will be removed in a future version and instead <see cref="ChangeSet{TObject, TKey}"/> will be used directly.</para>
    /// <para>In the mean time I am banking that no-one has implemented a custom change set - personally I think it is very unlikely.</para>
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

    /// <summary>
    /// Transforms the change set into a different type using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TKey">The type of the Key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformer">The transformer.</param>
    /// <returns>The change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// transformer.
    /// </exception>
    public static IChangeSet<TDestination, TKey> Transform<TSource, TDestination, TKey>(this IChangeSet<TSource, TKey> source, Func<TSource, TDestination> transformer)
        where TSource : notnull
        where TDestination : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformer.ThrowArgumentNullExceptionIfNull(nameof(transformer));

        var changes = source.Select(change =>
            new Change<TDestination, TKey>(change.Reason, change.Key, transformer(change.Current), change.Previous.Convert(transformer), change.CurrentIndex, change.PreviousIndex));

        return new ChangeSet<TDestination, TKey>(changes);
    }
}
