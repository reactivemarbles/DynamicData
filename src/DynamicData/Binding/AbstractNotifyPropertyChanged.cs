// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;

namespace DynamicData.Binding;

/// <summary>
/// Base class for implementing notify property changes.
/// </summary>
public abstract class AbstractNotifyPropertyChanged : INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a property value has changed.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Invokes on property changed.
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// If the value has changed, sets referenced backing field and raise notify property changed.
    /// </summary>
    /// <typeparam name="T">The type to set and raise.</typeparam>
    /// <param name="backingField">The backing field.</param>
    /// <param name="newValue">The new value.</param>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void SetAndRaise<T>(ref T backingField, T newValue, [CallerMemberName] string? propertyName = null) =>
        SetAndRaise(ref backingField, newValue, EqualityComparer<T>.Default, propertyName); // ReSharper disable once ExplicitCallerInfoArgument

    /// <summary>
    /// If the value has changed, sets referenced backing field and raise notify property changed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="backingField">The backing field.</param>
    /// <param name="newValue">The new value.</param>
    /// <param name="comparer">The comparer.</param>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void SetAndRaise<T>(ref T backingField, T newValue, IEqualityComparer<T>? comparer, [CallerMemberName] string? propertyName = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        if (comparer.Equals(backingField, newValue))
        {
            return;
        }

        backingField = newValue;
        OnPropertyChanged(propertyName);
    }
}
