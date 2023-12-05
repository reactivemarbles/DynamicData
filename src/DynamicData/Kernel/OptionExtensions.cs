// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;

namespace DynamicData.Kernel;

/// <summary>
/// Extensions for optional.
/// </summary>
public static class OptionExtensions
{
    /// <summary>
    /// Converts the specified source.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="ArgumentNullException">converter.</exception>
    public static Optional<TDestination> Convert<TSource, TDestination>(this in Optional<TSource> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        converter.ThrowArgumentNullExceptionIfNull(nameof(converter));

        return source.HasValue ? converter(source.Value) : Optional.None<TDestination>();
    }

    /// <summary>
    /// Attempts to converts the specified source, but the conversion might result in a None value.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter that returns an optional value.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="ArgumentNullException">converter.</exception>
    public static Optional<TDestination> Convert<TSource, TDestination>(this in Optional<TSource> source, Func<TSource, Optional<TDestination>> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        converter.ThrowArgumentNullExceptionIfNull(nameof(converter));

        return source.HasValue ? converter(source.Value) : Optional.None<TDestination>();
    }

    /// <summary>
    /// Converts the option value if it has a value, otherwise returns the result of the fallback converter.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter.</param>
    /// <param name="fallbackConverter">The fallback converter.</param>
    /// <returns>The destination value.</returns>
    /// <exception cref="ArgumentNullException">
    /// converter
    /// or
    /// fallbackConverter.
    /// </exception>
    public static TDestination? ConvertOr<TSource, TDestination>(this in Optional<TSource> source, Func<TSource?, TDestination?> converter, Func<TDestination?> fallbackConverter)
        where TSource : notnull
    {
        converter.ThrowArgumentNullExceptionIfNull(nameof(converter));
        fallbackConverter.ThrowArgumentNullExceptionIfNull(nameof(fallbackConverter));

        return source.HasValue ? converter(source.Value) : fallbackConverter();
    }

    /// <summary>
    /// Returns the original optional if it has a value, otherwise returns the result of the fallback operation.
    /// </summary>
    /// <typeparam name="T">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="fallbackOperation">The fallback operation.</param>
    /// <returns>The original value or the result of the fallback operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// converter
    /// or
    /// fallbackOperation.
    /// </exception>
    public static Optional<T> OrElse<T>(this in Optional<T> source, Func<Optional<T>> fallbackOperation)
        where T : notnull
    {
        fallbackOperation.ThrowArgumentNullExceptionIfNull(nameof(fallbackOperation));

        return source.HasValue ? source : fallbackOperation();
    }

    /// <summary>
    /// Overloads Enumerable.FirstOrDefault() and wraps the result in a Optional<typeparam>
    ///         <name>&amp;gt;T</name>
    ///     </typeparam> container.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The first value or none.</returns>
    public static Optional<T> FirstOrOptional<T>(this IEnumerable<T> source, Func<T, bool> selector)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        selector.ThrowArgumentNullExceptionIfNull(nameof(selector));

        foreach (var item in source.Where(item => selector(item)))
        {
            return Optional<T>.Create(item);
        }

        return Optional.None<T>();
    }

    /// <summary>
    /// Invokes the specified action when.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <returns>The optional else extension.</returns>
    public static OptionElse IfHasValue<T>(this in Optional<T> source, Action<T> action)
        where T : notnull
    {
        if (!source.HasValue || source.Value is null)
        {
            return new OptionElse();
        }

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        action(source.Value);
        return OptionElse.NoAction;
    }

    /// <summary>
    /// Invokes the specified action when.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <returns>The optional else extension.</returns>
    public static OptionElse IfHasValue<T>(this Optional<T>? source, Action<T> action)
        where T : notnull
    {
        if (!source.HasValue)
        {
            return new OptionElse();
        }

        if (!source.Value.HasValue)
        {
            return new OptionElse();
        }

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        action(source.Value.Value);
        return OptionElse.NoAction;
    }

    /// <summary>
    /// Overloads a TryGetValue of the dictionary wrapping the result as an Optional.<typeparam>
    ///         <name>&amp;gt;TValue</name>
    ///     </typeparam>
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    /// <returns>The option of the looked up value.</returns>
    public static Optional<TValue> Lookup<TValue, TKey>(this IDictionary<TKey, TValue> source, TKey key)
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var result = source.TryGetValue(key, out var contained);
        return result ? contained : Optional.None<TValue>();
    }

    /// <summary>
    /// Removes item if contained in the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    /// <returns>If the item was removed.</returns>
    public static bool RemoveIfContained<TValue, TKey>(this IDictionary<TKey, TValue> source, TKey key)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.ContainsKey(key) && source.Remove(key);
    }

    /// <summary>
    /// Filters where Optional has a value
    /// and return the values only.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An enumerable of the selected items.</returns>
    public static IEnumerable<T> SelectValues<T>(this IEnumerable<Optional<T>> source)
        where T : notnull => source.Where(t => t.HasValue && t.Value is not null).Select(t => t.Value!);

    /// <summary>
    /// Returns the value if the nullable has a value, otherwise returns the result of the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The value or the default value.</returns>
    public static T ValueOr<T>(this T? source, T defaultValue)
        where T : struct => source ?? defaultValue;

    /// <summary>
    /// Returns the value if the optional has a value, otherwise returns the result of the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>If the value or a provided default.</returns>
    /// <exception cref="ArgumentNullException">valueSelector.</exception>
    public static T ValueOr<T>(this in Optional<T> source, Func<T> valueSelector)
        where T : notnull
    {
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.HasValue ? source.Value : valueSelector();
    }

    /// <summary>
    /// Returns the value if the optional has a value, otherwise returns the default value of T.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The value or default.</returns>
    public static T? ValueOrDefault<T>(this in Optional<T> source)
        where T : notnull
    {
        if (source.HasValue)
        {
            return source.Value;
        }

        return default;
    }

    /// <summary>
    /// Returns the value if the optional has a value, otherwise throws an exception as specified by the exception generator.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="exceptionGenerator">The exception generator.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">exceptionGenerator.</exception>
    public static T ValueOrThrow<T>(this in Optional<T> source, Func<Exception> exceptionGenerator)
        where T : notnull
    {
        exceptionGenerator.ThrowArgumentNullExceptionIfNull(nameof(exceptionGenerator));

        if (source.HasValue && source.Value is not null)
        {
            return source.Value;
        }

        throw exceptionGenerator();
    }
}
