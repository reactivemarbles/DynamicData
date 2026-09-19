// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the Transformer class.
/// </summary>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
internal sealed class Transformer<TSource, TDestination>
    where TSource : notnull
    where TDestination : notnull
{
    /// <summary>
    /// The _containerFactory field.
    /// </summary>
    private readonly Func<TSource, ReactiveUI.Primitives.Optional<TDestination>, int, TransformedItemContainer> _containerFactory;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TSource>> _source;

    /// <summary>
    /// The _transformOnRefresh field.
    /// </summary>
    private readonly bool _transformOnRefresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="Transformer{TSource, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="factory">The factory value.</param>
    /// <param name="transformOnRefresh">The transformOnRefresh value.</param>
    public Transformer(IObservable<IChangeSet<TSource>> source, Func<TSource, ReactiveUI.Primitives.Optional<TDestination>, int, TDestination> factory, bool transformOnRefresh)
    {
        ArgumentExceptionHelper.ThrowIfNull(factory);
        ArgumentExceptionHelper.ThrowIfNull(source);

        _source = source;
        _transformOnRefresh = transformOnRefresh;
        _containerFactory = (item, prev, index) => new TransformedItemContainer(item, factory(item, prev, index));
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination>> Run() => Observable.Defer(RunImpl);

    /// <summary>
    /// Executes the RunImpl operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TDestination>> RunImpl() => _source.Scan(new ChangeAwareList<TransformedItemContainer>(), (state, changes) =>
            {
                Transform(state, changes);
                return state;
            })
            .Select(transformed =>
            {
                var changed = transformed.CaptureChanges();
                return changed.Transform(container => container.Destination);
            });

    /// <summary>
    /// Executes the Transform operation.
    /// </summary>
    /// <param name="transformed">The transformed value.</param>
    /// <param name="changes">The changes value.</param>
    private void Transform(ChangeAwareList<TransformedItemContainer> transformed, IChangeSet<TSource> changes)
    {
        ArgumentExceptionHelper.ThrowIfNull(changes);

        foreach (var item in changes)
        {
            switch (item.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (change.CurrentIndex < 0 || change.CurrentIndex >= transformed.Count)
                        {
                            transformed.Add(_containerFactory(change.Current, ReactiveUI.Primitives.Optional<TDestination>.None, transformed.Count));
                        }
                        else
                        {
                            var converted = _containerFactory(change.Current, ReactiveUI.Primitives.Optional<TDestination>.None, change.CurrentIndex);
                            transformed.Insert(change.CurrentIndex, converted);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var startIndex = item.Range.Index < 0 ? transformed.Count : item.Range.Index;

                        transformed.AddOrInsertRange(item.Range.Select((t, idx) => _containerFactory(t, ReactiveUI.Primitives.Optional<TDestination>.None, idx + startIndex)), item.Range.Index);

                        break;
                    }

                case ListChangeReason.Refresh:
                    {
                        var change = item.Item;
                        var index = change.CurrentIndex;
                        if (index < 0)
                        {
                            // Find the corresponding index
                            var current = transformed.FirstOrDefault(x => x.Source.Equals(change.Current));
                            index = current switch
                            {
                                { } tic when transformed.IndexOf(tic) is int i and (>= 0) => i,
                                _ => throw new UnspecifiedIndexException($"Cannot find index of {change.Current}"),
                            };
                        }

                        if (_transformOnRefresh)
                        {
                            ReactiveUI.Primitives.Optional<TDestination> previous = transformed[index].Destination;
                            transformed[index] = _containerFactory(change.Current, previous, index);
                        }
                        else
                        {
                            transformed.RefreshAt(index);
                        }

                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;

                        if (change.CurrentIndex == -1 || change.PreviousIndex == -1)
                        {
                            // Find the original, with it's corresponding index
                            var previous = transformed.First(x => x.Source.Equals(change.Previous.Value));
                            var index = transformed.IndexOf(previous);
                            if (index == -1)
                            {
                                throw new UnspecifiedIndexException($"Cannot find index of {change.Previous.Value}");
                            }

                            transformed[index] = _containerFactory(change.Current, previous.Destination, index);
                        }
                        else
                        {
                            ReactiveUI.Primitives.Optional<TDestination> previous = transformed[change.PreviousIndex].Destination;
                            if (change.CurrentIndex == change.PreviousIndex)
                            {
                                transformed[change.CurrentIndex] = _containerFactory(change.Current, previous, change.CurrentIndex);
                            }
                            else
                            {
                                transformed.RemoveAt(change.PreviousIndex);
                                transformed.Insert(change.CurrentIndex, _containerFactory(change.Current, ReactiveUI.Primitives.Optional<TDestination>.None, change.CurrentIndex));
                            }
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var change = item.Item;
                        var hasIndex = change.CurrentIndex >= 0;

                        if (hasIndex)
                        {
                            transformed.RemoveAt(change.CurrentIndex);
                        }
                        else
                        {
                            var toRemove = transformed.FirstOrDefault(t => ReferenceEquals(t.Source, change.Current));

                            if (toRemove is not null)
                            {
                                transformed.Remove(toRemove);
                            }
                        }

                        break;
                    }

                case ListChangeReason.RemoveRange:
                    {
                        if (item.Range.Index >= 0)
                        {
                            transformed.RemoveRange(item.Range.Index, item.Range.Count);
                        }
                        else
                        {
                            var toRemove = transformed.Where(t => item.Range.Any(current => ReferenceEquals(t.Source, current)));
                            transformed.RemoveMany(toRemove);
                        }

                        break;
                    }

                case ListChangeReason.Clear:
                    {
                        // i.e. need to store transformed reference so we can correctly clear
                        var toClear = new Change<TransformedItemContainer>(ListChangeReason.Clear, transformed);
                        transformed.ClearOrRemoveMany(toClear);

                        break;
                    }

                case ListChangeReason.Moved:
                    {
                        var change = item.Item;
                        var hasIndex = change.CurrentIndex >= 0;
                        if (!hasIndex)
                        {
                            throw new UnspecifiedIndexException("Cannot move as an index was not specified");
                        }

                        transformed.Move(change.PreviousIndex, change.CurrentIndex);
                        break;
                    }
            }
        }
    }

/// <summary>
/// Provides members for the TransformedItemContainer class.
/// </summary>
/// <param name="source">The source value.</param>
/// <param name="destination">The destination value.</param>
internal sealed class TransformedItemContainer(TSource source, TDestination destination) : IEquatable<TransformedItemContainer>
    {
        /// <summary>
        /// Gets the Destination value.
        /// </summary>
        public TDestination Destination { get; } = destination;

        /// <summary>
        /// Gets the Source value.
        /// </summary>
        public TSource Source { get; } = source;

        /// <summary>
        /// Executes the operator operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator ==(TransformedItemContainer left, TransformedItemContainer right) => Equals(left, right);

        /// <summary>
        /// Executes the operator operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator !=(TransformedItemContainer left, TransformedItemContainer right) => !Equals(left, right);

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Equals(TransformedItemContainer? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<TSource>.Default.Equals(Source, other.Source);
        }

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((TransformedItemContainer)obj);
        }

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override int GetHashCode() => Source is null ? 0 : EqualityComparer<TSource>.Default.GetHashCode(Source);
    }
}
