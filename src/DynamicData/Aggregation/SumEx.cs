// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Aggregation extensions.
/// </summary>
public static class SumEx
{
    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, int&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>A Refresh re-evaluates the selector. Updates, replacements, and removals subtract the value last observed
    /// for that item, not a new projection of the previous or removed instance.</para>
    /// <para>Each subscription stores one projection per key, requiring O(n) memory.
    /// Use <c>ForAggregation().Sum(valueSelector)</c> for stateless aggregation when items never change in place;
    /// that path does not retain projections or re-evaluate refreshed items.</para>
    /// <list type="table">
    /// <listheader><term>Source event</term><description>Effect on the sum</description></listheader>
    /// <item><term>Add</term><description>Evaluates and stores the projection for the new key, then adds it to the sum.</description></item>
    /// <item><term>Update</term><description>Subtracts the stored projection and evaluates and stores the replacement projection.</description></item>
    /// <item><term>Remove</term><description>Subtracts the stored projection and removes the key from projection state.</description></item>
    /// <item><term>Refresh</term><description>Re-evaluates the selector, replacing the stored contribution with the current projection.</description></item>
    /// <item><term>Moved</term><description>Leaves the sum unchanged.</description></item>
    /// <item><term>OnError</term><description>Forwards the error and terminates the subscription.</description></item>
    /// <item><term>OnCompleted</term><description>Forwards completion immediately.</description></item>
    /// </list>
    /// <para>Selector exceptions and malformed changesets fault the stream. An Add must introduce a new key; Update, Refresh, and Remove require an existing key.</para>
    /// </remarks>
    public static IObservable<int> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, int?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<int> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, long&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<long> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, long?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<long> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, double&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<double> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, double?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<double> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, decimal&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<decimal> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, decimal?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<decimal> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, float&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<float> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="TObject">The type of items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;TObject, float?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, int})" path="/member/remarks"/>
    public static IObservable<float> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, int&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>A Refresh re-evaluates the selector. Updates, replacements, and removals subtract the value last observed
    /// for that item, not a new projection of the previous or removed instance.</para>
    /// <para>Each subscription stores one projection per list position, requiring O(n) memory.
    /// Use <c>ForAggregation().Sum(valueSelector)</c> for stateless aggregation when items never change in place;
    /// that path does not retain projections or re-evaluate refreshed items.</para>
    /// <list type="table">
    /// <listheader><term>Source event</term><description>Effect on the sum</description></listheader>
    /// <item><term>Add / AddRange</term><description>Evaluates and stores each new projection at its collection position and adds it to the sum.</description></item>
    /// <item><term>Replace</term><description>Subtracts the stored projection and evaluates and stores the replacement at its new position.</description></item>
    /// <item><term>Remove / RemoveRange</term><description>Subtracts the stored projections and removes their collection positions.</description></item>
    /// <item><term>Refresh</term><description>Re-evaluates the selector, replacing the stored contribution with the current projection.</description></item>
    /// <item><term>Moved</term><description>Updates the stored collection order without changing the sum.</description></item>
    /// <item><term>Clear</term><description>Resets the sum to zero and clears projection state without re-evaluating selectors.</description></item>
    /// <item><term>OnError</term><description>Forwards the error and terminates the subscription.</description></item>
    /// <item><term>OnCompleted</term><description>Forwards completion immediately.</description></item>
    /// </list>
    /// <para>Selector exceptions and malformed changesets fault the stream. Removals, replacements, and refreshes must identify existing items. Indexless lookup prefers reference identity, then equality.</para>
    /// </remarks>
    public static IObservable<int> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, int?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<int> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, int?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, long&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<long> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, long?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<long> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, long?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, double&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<double> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, double?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<double> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, double?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, decimal&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<decimal> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, decimal?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<decimal> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, float&gt;</see> that selects each item's contribution.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<float> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum of the selected values.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}">IObservable&lt;IChangeSet&lt;T&gt;&gt;</see> to sum.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}">Func&lt;T, float?&gt;</see> that selects each item's contribution, treating null as zero.</param>
    /// <returns>An observable that emits the current sum after each source changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <inheritdoc cref="Sum{T}(IObservable{IChangeSet{T}}, Func{T, int})" path="/member/remarks"/>
    public static IObservable<float> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, float?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector)
        => SumAggregate(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector)
        => SumAggregate(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector)
        => SumAggregate(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector)
        => SumAggregate(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector)
        => SumAggregate(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    private static IObservable<TValue> SumCacheStateful<TObject, TKey, TValue>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<TValue, TValue>(seed, add, subtract), static accumulator => accumulator.State);

    private static IObservable<TValue> SumCacheStatefulNullable<TObject, TKey, TValue>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TKey : notnull
        where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        return SumCacheStateful(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }

    private static IObservable<TValue> SumListStateful<TObject, TValue>(
        IObservable<IChangeSet<TObject>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<TValue, TValue>(seed, add, subtract), static accumulator => accumulator.State);

    private static IObservable<TValue> SumListStatefulNullable<TObject, TValue>(
        IObservable<IChangeSet<TObject>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        return SumListStateful(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }

    private static IObservable<TValue> SumAggregate<TObject, TValue>(
        IObservable<IAggregateChangeSet<TObject>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueSelector);

        return source.Scan(seed, (sum, changes) =>
        {
            foreach (var change in changes)
            {
                var value = valueSelector(change.Item);
                sum = change.Type == AggregateType.Add ? add(sum, value) : subtract(sum, value);
            }

            return sum;
        });
    }

    private static IObservable<TValue> SumAggregateNullable<TObject, TValue>(
        IObservable<IAggregateChangeSet<TObject>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        return SumAggregate(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }
}
