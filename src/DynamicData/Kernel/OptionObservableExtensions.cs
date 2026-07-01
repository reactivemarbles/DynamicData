// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

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
    /// <remarks>Observable version of <seealso><c>OptionExtensions.Convert&lt;TSource, TDestination&gt;(in Optional&lt;TSource&gt;, Func&lt;TSource, TDestination&gt;)</c></seealso>.</remarks>
    public static IObservable<ReactiveUI.Primitives.Optional<TDestination>> Convert<TSource, TDestination>(this IObservable<ReactiveUI.Primitives.Optional<TSource>> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(converter);

        return source.Select(optional => optional.HasValue ? converter(optional.Value) : ReactiveUI.Primitives.Optional<TDestination>.None);
    }

    /// <summary>
    /// Overload of <c>Convert&lt;TSource, TDestination&gt;(IObservable&lt;Optional&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;)</c> that allows the conversion
    /// operation to also return an ReactiveUI.Primitives.Optional.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="converter">The converter that returns an optional value.</param>
    /// <returns>Observable Optional of <typeparamref name="TDestination"/>.</returns>
    /// <exception cref="ArgumentNullException">Source or Converter was null.</exception>
    /// <remarks>Observable version of <seealso><c>OptionExtensions.Convert&lt;TSource, TDestination&gt;(in Optional&lt;TSource&gt;, Func&lt;TSource, Optional&lt;TDestination&gt;&gt;)</c></seealso>.</remarks>
    public static IObservable<ReactiveUI.Primitives.Optional<TDestination>> Convert<TSource, TDestination>(this IObservable<ReactiveUI.Primitives.Optional<TSource>> source, Func<TSource, ReactiveUI.Primitives.Optional<TDestination>> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(converter);

        return source.Select(optional => optional.HasValue ? converter(optional.Value) : ReactiveUI.Primitives.Optional<TDestination>.None);
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
    public static IObservable<TDestination?> ConvertOr<TSource, TDestination>(this IObservable<ReactiveUI.Primitives.Optional<TSource>> source, Func<TSource, TDestination?> converter, Func<TDestination?> fallbackConverter)
        where TSource : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(converter);
        ArgumentExceptionHelper.ThrowIfNull(fallbackConverter);

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
    /// <remarks>Observable version of <seealso><c>OptionExtensions.OrElse&lt;T&gt;(in Optional&lt;T&gt;, Func&lt;Optional&lt;T&gt;&gt;)</c></seealso>.</remarks>
    public static IObservable<ReactiveUI.Primitives.Optional<T>> OrElse<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source, Func<ReactiveUI.Primitives.Optional<T>> fallbackOperation)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(fallbackOperation);

        return source.Select(optional => optional.HasValue ? optional : fallbackOperation());
    }

    /// <summary>
    /// Pass-Thru operator that invokes the specified action for Optionals with a value (or, if provided, the else Action for those without a value).
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <param name="elseAction">Optional alternative action for the Else case.</param>
    /// <returns>The same Observable ReactiveUI.Primitives.Optional.</returns>
    /// <remarks>Observable version of <seealso><c>OptionExtensions.IfHasValue&lt;T&gt;(in Optional&lt;T&gt;, Action&lt;T&gt;)</c></seealso>.</remarks>
    public static IObservable<ReactiveUI.Primitives.Optional<T>> OnHasValue<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source, Action<T> action, Action? elseAction = null)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(optional => optional.IfHasValue(action).Else(() => elseAction?.Invoke()));
    }

    /// <summary>
    /// Pass-Thru operator that invokes the specified action for Optionals without a value (or, if provided, the else Action for those with a value).
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <param name="elseAction">Optional alternative action for the Else case.</param>
    /// <returns>The same Observable ReactiveUI.Primitives.Optional.</returns>
    public static IObservable<ReactiveUI.Primitives.Optional<T>> OnHasNoValue<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source, Action action, Action<T>? elseAction = null)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(optional => optional.IfHasValue(val => elseAction?.Invoke(val)).Else(action));
    }

    /// <summary>
    /// Converts an Observable of <c>Optional&lt;T&gt;</c> into an IObservable of <typeparamref name="T"/> by extracting
    /// the values from Optionals that have one.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An Observable with the Values.</returns>
    /// <remarks>Observable version of <seealso><c>OptionExtensions.SelectValues&lt;T&gt;(IEnumerable&lt;Optional&lt;T&gt;&gt;)</c></seealso>.</remarks>
    public static IObservable<T> SelectValues<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Where(t => t.HasValue && t.Value is not null).Select(t => t.Value!);
    }

    /// <summary>
    /// Converts an Observable of <c>Optional&lt;T&gt;</c> into an IObservable of <typeparamref name="T"/> by extracting the
    /// values from the ones that contain a value and then using <paramref name="valueSelector"/> to generate a value for the others.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>If the value or a provided default.</returns>
    /// <exception cref="ArgumentNullException">valueSelector.</exception>
    /// <remarks>Observable version of <seealso><c>OptionExtensions.ValueOr&lt;T&gt;(in Optional&lt;T&gt;, Func&lt;T&gt;)</c></seealso>.</remarks>
    public static IObservable<T> ValueOr<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source, Func<T> valueSelector)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.Select(optional => optional.HasValue ? optional.Value : valueSelector());
    }

    /// <summary>
    /// Returns the value if the optional has a value, otherwise returns the default value of T.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The value or default.</returns>
    /// <remarks>Observable version of <seealso><c>OptionExtensions.ValueOrDefault&lt;T&gt;(in Optional&lt;T&gt;)</c></seealso>.</remarks>
    public static IObservable<T?> ValueOrDefault<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Select(optional => optional.ValueOrDefault());
    }

    /// <summary>
    /// Converts an Observable of <c>Optional&lt;T&gt;</c> into an IObservable of <typeparamref name="T"/> by extracting the values.
    /// If it has no value, <paramref name="exceptionGenerator"/> is used to generate an exception that is injected into the stream as error.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="exceptionGenerator">The exception generator.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">exceptionGenerator.</exception>
    /// <remarks>Observable version of <seealso><c>OptionExtensions.ValueOrThrow&lt;T&gt;(in Optional&lt;T&gt;, Func&lt;Exception&gt;)</c></seealso>.</remarks>
    public static IObservable<T> ValueOrThrow<T>(this IObservable<ReactiveUI.Primitives.Optional<T>> source, Func<Exception> exceptionGenerator)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(exceptionGenerator);

        return Observable.Create<T>(observer =>
            source.Subscribe(
                optional => optional.IfHasValue(val => observer.OnNext(val)).Else(() => observer.OnError(exceptionGenerator())),
                observer.OnError,
                observer.OnCompleted));
    }
}
