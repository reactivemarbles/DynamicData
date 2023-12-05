// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Kernel;

/// <summary>
/// Extensions for optional.
/// </summary>
public static class OptionObservableExtensions
{
    /// <summary>
    /// Converts an Observable Optional of <typeparamref name="TSource"/> into an Observable Optional of <typeparamref name="TDestination"/> by applying
    /// the conversion function to those Optionals that have a value.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter.</param>
    /// <returns>Observable Optional of <typeparamref name="TDestination"/>.</returns>
    /// <exception cref="ArgumentNullException">Source or Converter was null.</exception>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.Convert{TSource, TDestination}(in Optional{TSource}, Func{TSource, TDestination})"/>.</remarks>
    public static IObservable<Optional<TDestination>> Convert<TSource, TDestination>(this IObservable<Optional<TSource>> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        converter.ThrowArgumentNullExceptionIfNull(nameof(converter));

        return source.Select(optional => optional.HasValue ? converter(optional.Value) : Optional.None<TDestination>());
    }

    /// <summary>
    /// Overload of <see cref="Convert{TSource, TDestination}(IObservable{Optional{TSource}}, Func{TSource, TDestination})"/> that allows the conversion
    /// operation to also return an Optional.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter that returns an optional value.</param>
    /// <returns>Observable Optional of <typeparamref name="TDestination"/>.</returns>
    /// <exception cref="ArgumentNullException">Source or Converter was null.</exception>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.Convert{TSource, TDestination}(in Optional{TSource}, Func{TSource, Optional{TDestination}})"/>.</remarks>
    public static IObservable<Optional<TDestination>> Convert<TSource, TDestination>(this IObservable<Optional<TSource>> source, Func<TSource, Optional<TDestination>> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        converter.ThrowArgumentNullExceptionIfNull(nameof(converter));

        return source.Select(optional => optional.HasValue ? converter(optional.Value) : Optional.None<TDestination>());
    }

    /// <summary>
    /// Converts an observable of optional into an observable of <typeparamref name="TDestination"/> by applying <paramref name="converter"/> to convert Optionals with a value
    /// and using <paramref name="fallbackConverter"/> to generate a value for those that don't have a value.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter.</param>
    /// <param name="fallbackConverter">The fallback converter.</param>
    /// <returns>Observable of <typeparamref name="TDestination"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// converter
    /// or
    /// fallbackConverter.
    /// </exception>
    public static IObservable<TDestination?> ConvertOr<TSource, TDestination>(this IObservable<Optional<TSource>> source, Func<TSource, TDestination?> converter, Func<TDestination?> fallbackConverter)
        where TSource : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        converter.ThrowArgumentNullExceptionIfNull(nameof(converter));
        fallbackConverter.ThrowArgumentNullExceptionIfNull(nameof(fallbackConverter));

        return source.Select(optional => optional.HasValue ? converter(optional.Value) : fallbackConverter());
    }

    /// <summary>
    /// Observable Optional operator that provides a way to (possibly) create a value for those Optionals that don't already have one.
    /// </summary>
    /// <typeparam name="T">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="fallbackOperation">The fallback operation.</param>
    /// <returns>An Observable Optional that contains the Optionals with Values or the results of the fallback operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// fallbackOperation.
    /// </exception>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.OrElse{T}(in Optional{T}, Func{Optional{T}})"/>.</remarks>
    public static IObservable<Optional<T>> OrElse<T>(this IObservable<Optional<T>> source, Func<Optional<T>> fallbackOperation)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        fallbackOperation.ThrowArgumentNullExceptionIfNull(nameof(fallbackOperation));

        return source.Select(optional => optional.HasValue ? optional : fallbackOperation());
    }

    /// <summary>
    /// Pass-Thru operator that invokes the specified action for Optionals with a value (or, if provided, the else Action for those without a value).
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <param name="elseAction">Optional alternative action for the Else case.</param>
    /// <returns>The same Observable Optional.</returns>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.IfHasValue{T}(in Optional{T}, Action{T})"/>.</remarks>
    public static IObservable<Optional<T>> OnHasValue<T>(this IObservable<Optional<T>> source, Action<T> action, Action? elseAction = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(optional => optional.IfHasValue(action).Else(() => elseAction?.Invoke()));
    }

    /// <summary>
    /// Pass-Thru operator that invokes the specified action for Optionals without a value (or, if provided, the else Action for those with a value).
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <param name="elseAction">Optional alternative action for the Else case.</param>
    /// <returns>The same Observable Optional.</returns>
    public static IObservable<Optional<T>> OnHasNoValue<T>(this IObservable<Optional<T>> source, Action action, Action<T>? elseAction = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(optional => optional.IfHasValue(val => elseAction?.Invoke(val)).Else(action));
    }

    /// <summary>
    /// Converts an Observable of <see cref="Optional{T}"/> into an IObservable of <typeparamref name="T"/> by extracting
    /// the values from Optionals that have one.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An Observable with the Values.</returns>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.SelectValues{T}(IEnumerable{Optional{T}})"/>.</remarks>
    public static IObservable<T> SelectValues<T>(this IObservable<Optional<T>> source)
        where T : notnull => source.Where(t => t.HasValue && t.Value is not null).Select(t => t.Value!);

    /// <summary>
    /// Converts an Observable of <see cref="Optional{T}"/> into an IObservable of <typeparamref name="T"/> by extracting the
    /// values from the ones that contain a value and then using <paramref name="valueSelector"/> to generate a value for the others.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>If the value or a provided default.</returns>
    /// <exception cref="ArgumentNullException">valueSelector.</exception>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.ValueOr{T}(in Optional{T}, Func{T})"/>.</remarks>
    public static IObservable<T> ValueOr<T>(this IObservable<Optional<T>> source, Func<T> valueSelector)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(optional => optional.HasValue ? optional.Value : valueSelector());
    }

    /// <summary>
    /// Returns the value if the optional has a value, otherwise returns the default value of T.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The value or default.</returns>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.ValueOrDefault{T}(in Optional{T})"/>.</remarks>
    public static IObservable<T?> ValueOrDefault<T>(this IObservable<Optional<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(optional => optional.ValueOrDefault());
    }

    /// <summary>
    /// Converts an Observable of <see cref="Optional{T}"/> into an IObservable of <typeparamref name="T"/> by extracting the values.
    /// If it has no value, <paramref name="exceptionGenerator"/> is used to generate an exception that is injected into the stream as error.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="exceptionGenerator">The exception generator.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">exceptionGenerator.</exception>
    /// <remarks>Observable version of <seealso cref="OptionExtensions.ValueOrThrow{T}(in Optional{T}, Func{Exception})"/>.</remarks>
    public static IObservable<T> ValueOrThrow<T>(this IObservable<Optional<T>> source, Func<Exception> exceptionGenerator)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        exceptionGenerator.ThrowArgumentNullExceptionIfNull(nameof(exceptionGenerator));

        return Observable.Create<T>(observer =>
            source.Subscribe(
                optional => optional.IfHasValue(val => observer.OnNext(val)).Else(() => observer.OnError(exceptionGenerator())),
                observer.OnError,
                observer.OnCompleted));
    }
}
