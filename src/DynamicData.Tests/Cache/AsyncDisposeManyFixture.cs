using System;
using System.Threading.Tasks;

namespace DynamicData.Tests.Cache;

public static partial class AsyncDisposeManyFixture
{
    public enum SourceType
    {
        Subject,
        Immediate
    }

    public enum ItemType
    {
        Plain,
        Disposable,
        AsyncDisposable,
        ImmediateAsyncDisposable
    }

    public abstract record ItemBase
    {
        public static ItemBase Create(
                ItemType    type,
                int         id,
                int         version)
            => type switch
            {
                ItemType.Plain                      => new PlainItem()
                {
                    Id      = id,
                    Version = version
                },
                ItemType.Disposable                 => new DisposableItem()
                {
                    Id      = id,
                    Version = version
                },
                ItemType.AsyncDisposable            => new AsyncDisposableItem()
                {
                    Id      = id,
                    Version = version
                },
                ItemType.ImmediateAsyncDisposable   => new ImmediateAsyncDisposableItem()
                {
                    Id      = id,
                    Version = version
                },
                _                                   => throw new ArgumentException($"{type} is not a valid {nameof(ItemType)} value", nameof(type))
            };

        public required int Id { get; init; }

        public required int Version { get; init; }

        public abstract bool CanBeDisposed { get; }

        public abstract bool HasBeenDisposed { get; }

        public abstract void CompleteDisposal();

        public abstract void FailDisposal(Exception error);
    }

    public sealed record PlainItem
        : ItemBase
    {
        public override bool CanBeDisposed
            => false;

        public override bool HasBeenDisposed
            => false;

        public override void CompleteDisposal() { }

        public override void FailDisposal(Exception error) { }
    }

    public sealed record DisposableItem
        : ItemBase, IDisposable
    {
        public override bool CanBeDisposed
            => true;

        public override bool HasBeenDisposed
            => _hasBeenDisposed;

        public override void CompleteDisposal() { }

        public override void FailDisposal(Exception error)
            => _disposeError = error;

        public void Dispose()
        {
            if (_disposeError is not null)
                #pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                throw _disposeError;
                #pragma warning restore CA1065 // Do not raise exceptions in unexpected locations

            _hasBeenDisposed = true;
        }

        private Exception?  _disposeError;
        private bool        _hasBeenDisposed;
    }

    public sealed record AsyncDisposableItem
        : ItemBase, IAsyncDisposable
    {
        public override bool CanBeDisposed
            => true;

        public override bool HasBeenDisposed
            => _hasBeenDisposed;

        public override void CompleteDisposal()
            => _disposeCompletionSource.SetResult();

        public override void FailDisposal(Exception error)
            => _disposeCompletionSource.SetException(error);

        public ValueTask DisposeAsync()
        {
            _hasBeenDisposed = true;

            return new(_disposeCompletionSource.Task);
        }

        private readonly TaskCompletionSource _disposeCompletionSource
            = new();
     
        private bool _hasBeenDisposed;
    }

    public sealed record ImmediateAsyncDisposableItem
        : ItemBase, IAsyncDisposable
    {
        public override bool CanBeDisposed
            => true;

        public override bool HasBeenDisposed
            => _hasBeenDisposed;

        public override void CompleteDisposal() { }

        public override void FailDisposal(Exception error)
            => _disposeError = error;

        public ValueTask DisposeAsync()
        {
            _hasBeenDisposed = true;

            return (_disposeError is not null)
                ? ValueTask.FromException(_disposeError)
                : ValueTask.CompletedTask;
        }

        private Exception?  _disposeError;
        private bool        _hasBeenDisposed;
    }
}
