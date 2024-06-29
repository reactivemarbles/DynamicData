// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class ToObservableOptional<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly TKey _key = key;

    public IObservable<Optional<TObject>> Run() => Observable.Create<Optional<TObject>>(observer =>
                                                            _source.Subscribe(
                                                                changes =>
                                                                changes.Where(ShouldEmitChange).ForEach(change => observer.OnNext(change switch
                                                                {
                                                                    { Reason: ChangeReason.Remove } => Optional.None<TObject>(),
                                                                    _ => Optional.Some(change.Current),
                                                                })),
                                                                observer.OnError,
                                                                observer.OnCompleted));

    private bool ShouldEmitChange(Change<TObject, TKey> change) => change switch
    {
        { Key: { } thekey } when !thekey.Equals(_key) => false,
        { Reason: ChangeReason.Add } => true,
        { Reason: ChangeReason.Remove } => true,
        { Reason: ChangeReason.Update, Previous.HasValue: false } => true,
        { Reason: ChangeReason.Update } when equalityComparer is not null => !equalityComparer.Equals(change.Current, change.Previous.Value),
        { Reason: ChangeReason.Update } => !ReferenceEquals(change.Current, change.Previous.Value),
        _ => false,
    };
}
