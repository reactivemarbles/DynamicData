// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class ReaderWriter<T>
    where T : notnull
{
    private readonly object _locker = new();

    private ChangeAwareList<T> _data = new();

    private bool _updateInProgress;

    public int Count
    {
        get
        {
            lock (_locker)
            {
                return _data.Count;
            }
        }
    }

    public T[] Items
    {
        get
        {
            lock (_locker)
            {
                var result = new T[_data.Count];
                _data.CopyTo(result, 0);
                return result;
            }
        }
    }

    public IChangeSet<T> Write(IChangeSet<T> changes)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        IChangeSet<T> result;

        lock (_locker)
        {
            _data.Clone(changes);
            result = _data.CaptureChanges();
        }

        return result;
    }

    public IChangeSet<T> Write(Action<IExtendedList<T>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        IChangeSet<T> result;

        // Write straight to the list, no preview
        lock (_locker)
        {
            _updateInProgress = true;
            updateAction(_data);
            _updateInProgress = false;
            result = _data.CaptureChanges();
        }

        return result;
    }

    /// <summary>
    /// Perform a recursive write operation.
    /// Changes are added to the topmost change tracker.
    /// Use only during an invocation of Write/WriteWithPreview.
    /// </summary>
    /// <param name="updateAction">The action to perform on the list.</param>
    public void WriteNested(Action<IExtendedList<T>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        lock (_locker)
        {
            if (!_updateInProgress)
            {
                throw new InvalidOperationException("WriteNested can only be used if another write is already in progress.");
            }

            updateAction(_data);
        }
    }

    public IChangeSet<T> WriteWithPreview(Action<IExtendedList<T>> updateAction, Action<IChangeSet<T>> previewHandler)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        previewHandler.ThrowArgumentNullExceptionIfNull(nameof(previewHandler));

        IChangeSet<T> result;

        // Make a copy, apply changes on the main list, perform the preview callback with the old list and swap the lists again to finalize the update.
        lock (_locker)
        {
            ChangeAwareList<T> copy = new(_data, false);

            _updateInProgress = true;
            updateAction(_data);
            _updateInProgress = false;

            result = _data.CaptureChanges();

            InternalEx.Swap(ref _data, ref copy);

            previewHandler(result);

            InternalEx.Swap(ref _data, ref copy);
        }

        return result;
    }
}
