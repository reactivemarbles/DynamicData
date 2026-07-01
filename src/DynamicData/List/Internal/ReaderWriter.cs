// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the ReaderWriter class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class ReaderWriter<T>
    where T : notnull
{
    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _data field.
    /// </summary>
    private ChangeAwareList<T> _data = new();

    /// <summary>
    /// The _updateInProgress field.
    /// </summary>
    private bool _updateInProgress;

    /// <summary>
    /// Gets the Count value.
    /// </summary>
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

    /// <summary>
    /// Gets the Items value.
    /// </summary>
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

    /// <summary>
    /// Executes the Write operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    /// <returns>The result of the operation.</returns>
    public IChangeSet<T> Write(IChangeSet<T> changes)
    {
        ArgumentExceptionHelper.ThrowIfNull(changes);

        IChangeSet<T> result;

        lock (_locker)
        {
            _data.Clone(changes);
            result = _data.CaptureChanges();
        }

        return result;
    }

    /// <summary>
    /// Executes the Write operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    /// <returns>The result of the operation.</returns>
    public IChangeSet<T> Write(Action<IExtendedList<T>> updateAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

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
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        lock (_locker)
        {
            if (!_updateInProgress)
            {
                throw new InvalidOperationException("WriteNested can only be used if another write is already in progress.");
            }

            updateAction(_data);
        }
    }

    /// <summary>
    /// Executes the WriteWithPreview operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    /// <param name="previewHandler">The previewHandler value.</param>
    /// <returns>The result of the operation.</returns>
    public IChangeSet<T> WriteWithPreview(Action<IExtendedList<T>> updateAction, Action<IChangeSet<T>> previewHandler)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);
        ArgumentExceptionHelper.ThrowIfNull(previewHandler);

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
