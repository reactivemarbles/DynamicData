using System;

namespace DynamicData.Tests.Utilities;

internal static class FunctionalExtensions
{
    public static T With<T>(this T item, Action<T> action)
    {
        action(item);
        return item;
    }
}
