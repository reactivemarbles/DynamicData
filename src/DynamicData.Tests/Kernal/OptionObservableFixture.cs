using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Kernal;

public class OptionObservableFixture
{
    private const int NoneCount = 5;
    private const int SomeCount = 10;

    private static Optional<string> NotConvertableToInt { get; } = Optional.Some("NOT AN INT");
    private static IEnumerable<int> IntEnum { get; } = Enumerable.Range(0, SomeCount);
    private static IEnumerable<string> StringEnum { get; } = IntEnum.Select(n => n.ToString());
    private static IEnumerable<Optional<int>> OptIntEnum { get; } = IntEnum.Select(i => Optional.Some(i));
    private static IEnumerable<Optional<int>> OptNoneIntEnum { get; } = Enumerable.Repeat(Optional.None<int>(), NoneCount);
    private static IEnumerable<Optional<string>> OptNoneStringEnum { get; } = Enumerable.Repeat(Optional.None<string>(), NoneCount);
    private static IEnumerable<Optional<string>> OptStringEnum { get; } = StringEnum.Select(str => Optional.Some(str));
    private static IEnumerable<Optional<string>> OptStringWithNoneEnum { get; } = OptNoneStringEnum.Concat(OptStringEnum);
    private static IEnumerable<Optional<string>> OptStringWithBadEnum { get; } = OptStringEnum.Prepend(NotConvertableToInt);
    private static IEnumerable<Optional<string>> OptStringWithBadAndNoneEnum { get; } = OptStringWithNoneEnum.Prepend(NotConvertableToInt);

    [Fact]
    public void NullChecks()
    {
        // having
        var neverObservable = Observable.Never<Optional<int>>();
        var nullObservable = (IObservable<Optional<int>>)null!;
        var nullConverter = (Func<int, double>)null!;
        var nullOptionalConverter = (Func<int, Optional<double>>)null!;
        var converter = (Func<int, double>)(i => i);
        var nullFallback = (Func<int>)null!;
        var nullConvertFallback = (Func<double>)null!;
        var nullOptionalFallback = (Func<Optional<int>>)null!;
        var action = (Action)null!;
        var actionVal = (Action<int>)null!;
        var nullExceptionGenerator = (Func<Exception>)null!;

        // when
        var convert1 = () => nullObservable.Convert(nullConverter);
        var convert2 = () => neverObservable.Convert(nullConverter);
        var convertOpt1 = () => nullObservable.Convert(nullOptionalConverter);
        var convertOpt2 = () => neverObservable.Convert(nullOptionalConverter);
        var convertOr1 = () => nullObservable.ConvertOr(nullConverter, nullConvertFallback);
        var convertOr2 = () => neverObservable.ConvertOr(nullConverter, nullConvertFallback);
        var convertOr3 = () => neverObservable.ConvertOr(converter, nullConvertFallback);
        var orElse1 = () => nullObservable.OrElse(nullOptionalFallback);
        var orElse2 = () => neverObservable.OrElse(nullOptionalFallback);
        var onHasValue = () => nullObservable.OnHasValue(actionVal);
        var onHasValue2 = () => neverObservable.OnHasValue(actionVal);
        var onHasNoValue = () => nullObservable.OnHasNoValue(action);
        var onHasNoValue2 = () => neverObservable.OnHasNoValue(action);
        var selectValues = () => nullObservable.SelectValues();
        var valueOr = () => nullObservable.ValueOr(nullFallback);
        var valueOrDefault = () => nullObservable.ValueOrDefault();
        var valueOrThrow1 = () => nullObservable.ValueOrThrow(nullExceptionGenerator);
        var valueOrThrow2 = () => neverObservable.ValueOrThrow(nullExceptionGenerator);

        // then
        convert1.Should().Throw<ArgumentNullException>();
        convert2.Should().Throw<ArgumentNullException>();
        convertOpt1.Should().Throw<ArgumentNullException>();
        convertOpt2.Should().Throw<ArgumentNullException>();
        convertOr1.Should().Throw<ArgumentNullException>();
        convertOr2.Should().Throw<ArgumentNullException>();
        convertOr3.Should().Throw<ArgumentNullException>();
        orElse1.Should().Throw<ArgumentNullException>();
        orElse2.Should().Throw<ArgumentNullException>();
        onHasValue.Should().Throw<ArgumentNullException>();
        onHasValue2.Should().Throw<ArgumentNullException>();
        onHasNoValue.Should().Throw<ArgumentNullException>();
        onHasNoValue2.Should().Throw<ArgumentNullException>();
        selectValues.Should().Throw<ArgumentNullException>();
        valueOr.Should().Throw<ArgumentNullException>();
        valueOrDefault.Should().Throw<ArgumentNullException>();
        valueOrThrow1.Should().Throw<ArgumentNullException>();
        valueOrThrow2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConvertWillConvertValues()
    {
        // having
        var observable = OptStringEnum.ToObservable();

        // when
        var results = observable.Convert(ParseInt).ToEnumerable().ToList();
        var intList = OptIntEnum.ToList();

        // then
        results.Should().BeSubsetOf(intList);
        intList.Should().BeSubsetOf(results);
    }

    [Fact]
    public void ConvertPreservesNone()
    {
        // having
        var enumerable = OptStringWithNoneEnum;
        var observable = enumerable.ToObservable();

        // when
        var results = observable.Convert(ParseInt).Where(opt => !opt.HasValue).ToEnumerable().Count();
        var expected = enumerable.Where(opt => !opt.HasValue).Count();

        // then
        results.Should().Be(expected);
        results.Should().Be(NoneCount);
    }

    [Fact]
    public void ConvertOptionalWillConvertValues()
    {
        // having
        var observable = OptStringWithBadEnum.ToObservable();

        // when
        var results = observable.Convert(ParseIntOpt).ToEnumerable().ToList();
        var intList = OptIntEnum.ToList();

        // then
        intList.Should().BeSubsetOf(results);
        results.Should().Contain(Optional.None<int>());
    }

    [Fact]
    public void ConvertOptionalPreservesNone()
    {
        // having
        var enumerable = OptStringWithBadAndNoneEnum;
        var observable = enumerable.ToObservable();

        // when
        var results = observable.Convert(ParseIntOpt).Where(opt => !opt.HasValue).ToEnumerable().Count();
        var expected = OptStringWithNoneEnum.Where(opt => !opt.HasValue).Count() + 1;

        // then
        results.Should().Be(expected);
        results.Should().BeGreaterThan(1);
    }

    [Fact]
    public void ConvertOrConvertsOrFallsback()
    {
        // having
        var observable = OptStringWithNoneEnum.ToObservable();

        // when
        var results = observable.ConvertOr(ParseInt, () => -1).ToEnumerable();
        var intList = IntEnum.Prepend(-1);

        // then
        results.Should().BeSubsetOf(intList);
        intList.Should().BeSubsetOf(results);
    }

    [Fact]
    public void OrElseFallsback()
    {
        // having
        var observable = OptIntEnum.ToObservable().StartWith(Optional.None<int>());

        // when
        var results = observable.OrElse(() => -1).ToEnumerable();
        var intList = OptIntEnum.Prepend(-1);

        // then
        results.Should().BeSubsetOf(intList);
        intList.Should().BeSubsetOf(results);
    }

    [Fact]
    public void OnHasValueInvokesCorrectAction()
    {
        // having
        int value = 0;
        int noValue = 0;
        Action<int> onVal = _ => value++;
        Action onNoVal = () => noValue++;
        var observable = OptIntEnum.Concat(OptNoneIntEnum).ToObservable().OnHasValue(onVal, onNoVal);

        // when
        var results = observable.ToEnumerable().ToList();

        // then
        value.Should().Be(SomeCount);
        noValue.Should().Be(NoneCount);
    }

    [Fact]
    public void OnHasNoValueInvokesCorrectAction()
    {
        // having
        int value = 0;
        int noValue = 0;
        Action<int> onVal = _ => value++;
        Action onNoVal = () => noValue++;
        var observable = OptIntEnum.Concat(OptNoneIntEnum).ToObservable().OnHasNoValue(onNoVal, onVal);

        // when
        var results = observable.ToEnumerable().ToList();

        // then
        value.Should().Be(SomeCount);
        noValue.Should().Be(NoneCount);
    }

    [Fact]
    public void SelectValuesReturnsTheValues()
    {
        // having
        var enumerable = OptIntEnum.Concat(OptNoneIntEnum);
        var observable = enumerable.ToObservable().SelectValues();

        // when
        var expected = enumerable.Where(opt => opt.HasValue).Count();
        var results = observable.ToEnumerable().Count();

        // then
        expected.Should().Be(results);
        results.Should().Be(SomeCount);
    }

    [Fact]
    public void ValueOrInvokesSelector()
    {
        // having
        int invokeCount = 0;
        Func<int> selector = () => { invokeCount++; return -1; };
        var enumerable = OptIntEnum.Concat(OptNoneIntEnum);
        var observable = enumerable.ToObservable().ValueOr(selector);

        // when
        var expected = enumerable.Where(opt => !opt.HasValue).Count();
        var results = observable.ToEnumerable().Where(i => i.Equals(-1)).Count();

        // then
        expected.Should().Be(results);
        results.Should().Be(NoneCount);
        invokeCount.Should().Be(NoneCount);
    }

    [Fact]
    public void ValueOrDefaultReturnsDefaultValues()
    {
        // having
        var enumerable = OptStringWithNoneEnum;
        var observable = enumerable.ToObservable().ValueOrDefault();

        // when
        var expected = enumerable.Where(opt => !opt.HasValue).Count();
        var results = observable.ToEnumerable().Where(str => str == default).Count();

        // then
        expected.Should().Be(results);
        results.Should().Be(NoneCount);
    }

    [Fact]
    public void ValueOrThrowFailsWithGeneratedError()
    {
        // having
        var expectedError = new Exception("Nope");
        var exceptionGenerator = () => expectedError;
        var enumerable = OptStringWithNoneEnum;
        var observable = enumerable.ToObservable().ValueOrThrow(exceptionGenerator);
        var receivedError = default(Exception);

        // when
        using var cleanup = observable.Subscribe(_ => { }, err => receivedError = err);

        // then
        receivedError.Should().Be(expectedError);
    }

    private static Optional<int> ParseIntOpt(string input) =>
        int.TryParse(input, out var result) ? Optional.Some(result) : Optional.None<int>();

    private static int ParseInt(string input) => int.Parse(input);
}
