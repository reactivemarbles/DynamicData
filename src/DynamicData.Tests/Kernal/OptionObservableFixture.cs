#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.Kernal;

public class OptionObservableFixture
{
    private const int NoneCount = 5;
    private const int SomeCount = 10;

    private static ReactiveUI.Primitives.Optional<string> NotConvertableToInt { get; } = ReactiveUI.Primitives.Optional<string>.Some("NOT AN INT");
    private static IEnumerable<int> IntEnum { get; } = Enumerable.Range(0, SomeCount);
    private static IEnumerable<string> StringEnum { get; } = IntEnum.Select(n => n.ToString());
    private static IEnumerable<ReactiveUI.Primitives.Optional<int>> OptIntEnum { get; } = IntEnum.Select(i => ReactiveUI.Primitives.Optional<int>.Some(i));
    private static IEnumerable<ReactiveUI.Primitives.Optional<int>> OptNoneIntEnum { get; } = Enumerable.Repeat(ReactiveUI.Primitives.Optional<int>.None, NoneCount);
    private static IEnumerable<ReactiveUI.Primitives.Optional<string>> OptNoneStringEnum { get; } = Enumerable.Repeat(ReactiveUI.Primitives.Optional<string>.None, NoneCount);
    private static IEnumerable<ReactiveUI.Primitives.Optional<string>> OptStringEnum { get; } = StringEnum.Select(str => ReactiveUI.Primitives.Optional<string>.Some(str));
    private static IEnumerable<ReactiveUI.Primitives.Optional<string>> OptStringWithNoneEnum { get; } = OptNoneStringEnum.Concat(OptStringEnum);
    private static IEnumerable<ReactiveUI.Primitives.Optional<string>> OptStringWithBadEnum { get; } = OptStringEnum.Prepend(NotConvertableToInt);
    private static IEnumerable<ReactiveUI.Primitives.Optional<string>> OptStringWithBadAndNoneEnum { get; } = OptStringWithNoneEnum.Prepend(NotConvertableToInt);

    [Test]
    public async Task NullChecks()
    {
        // having
        var neverObservable = Observable.Never<ReactiveUI.Primitives.Optional<int>>();
        var nullObservable = (IObservable<ReactiveUI.Primitives.Optional<int>>)null!;
        var nullConverter = (Func<int, double>)null!;
        var nullOptionalConverter = (Func<int, ReactiveUI.Primitives.Optional<double>>)null!;
        var converter = (Func<int, double>)(i => i);
        var nullFallback = (Func<int>)null!;
        var nullConvertFallback = (Func<double>)null!;
        var nullOptionalFallback = (Func<ReactiveUI.Primitives.Optional<int>>)null!;
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
        await Assert.That(convert1).Throws<ArgumentNullException>();
        await Assert.That(convert2).Throws<ArgumentNullException>();
        await Assert.That(convertOpt1).Throws<ArgumentNullException>();
        await Assert.That(convertOpt2).Throws<ArgumentNullException>();
        await Assert.That(convertOr1).Throws<ArgumentNullException>();
        await Assert.That(convertOr2).Throws<ArgumentNullException>();
        await Assert.That(convertOr3).Throws<ArgumentNullException>();
        await Assert.That(orElse1).Throws<ArgumentNullException>();
        await Assert.That(orElse2).Throws<ArgumentNullException>();
        await Assert.That(onHasValue).Throws<ArgumentNullException>();
        await Assert.That(onHasValue2).Throws<ArgumentNullException>();
        await Assert.That(onHasNoValue).Throws<ArgumentNullException>();
        await Assert.That(onHasNoValue2).Throws<ArgumentNullException>();
        await Assert.That(selectValues).Throws<ArgumentNullException>();
        await Assert.That(valueOr).Throws<ArgumentNullException>();
        await Assert.That(valueOrDefault).Throws<ArgumentNullException>();
        await Assert.That(valueOrThrow1).Throws<ArgumentNullException>();
        await Assert.That(valueOrThrow2).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ConvertWillConvertValues()
    {
        // having
        var observable = OptStringEnum.ToObservable();

        // when
        var results = observable.Convert(ParseInt).ToEnumerable().ToList();
        var intList = OptIntEnum.ToList();

        // then
        await Assert.That(new HashSet<ReactiveUI.Primitives.Optional<int>>(results).IsSubsetOf(intList)).IsTrue();
        await Assert.That(new HashSet<ReactiveUI.Primitives.Optional<int>>(intList).IsSubsetOf(results)).IsTrue();
    }

    [Test]
    public async Task ConvertPreservesNone()
    {
        // having
        var enumerable = OptStringWithNoneEnum;
        var observable = enumerable.ToObservable();

        // when
        var results = observable.Convert(ParseInt).Where(opt => !opt.HasValue).ToEnumerable().Count();
        var expected = enumerable.Where(opt => !opt.HasValue).Count();

        // then
        await Assert.That(results).IsEqualTo(expected);
        await Assert.That(results).IsEqualTo(NoneCount);
    }

    [Test]
    public async Task ConvertOptionalWillConvertValues()
    {
        // having
        var observable = OptStringWithBadEnum.ToObservable();

        // when
        var results = observable.Convert(ParseIntOpt).ToEnumerable().ToList();
        var intList = OptIntEnum.ToList();

        // then
        await Assert.That(new HashSet<ReactiveUI.Primitives.Optional<int>>(intList).IsSubsetOf(results)).IsTrue();
        await Assert.That(results).Contains(ReactiveUI.Primitives.Optional<int>.None);
    }

    [Test]
    public async Task ConvertOptionalPreservesNone()
    {
        // having
        var enumerable = OptStringWithBadAndNoneEnum;
        var observable = enumerable.ToObservable();

        // when
        var results = observable.Convert(ParseIntOpt).Where(opt => !opt.HasValue).ToEnumerable().Count();
        var expected = OptStringWithNoneEnum.Where(opt => !opt.HasValue).Count() + 1;

        // then
        await Assert.That(results).IsEqualTo(expected);
        await Assert.That(results).IsGreaterThan(1);
    }

    [Test]
    public async Task ConvertOrConvertsOrFallsback()
    {
        // having
        var observable = OptStringWithNoneEnum.ToObservable();

        // when
        var results = observable.ConvertOr(ParseInt, () => -1).ToEnumerable();
        var intList = IntEnum.Prepend(-1);

        // then
        await Assert.That(new HashSet<int>(results).IsSubsetOf(intList)).IsTrue();
        await Assert.That(new HashSet<int>(intList).IsSubsetOf(results)).IsTrue();
    }

    [Test]
    public async Task OrElseFallsback()
    {
        // having
        var observable = OptIntEnum.ToObservable().StartWith(ReactiveUI.Primitives.Optional<int>.None);

        // when
        var results = observable.OrElse(() => -1).ToEnumerable();
        var intList = OptIntEnum.Prepend(-1);

        // then
        await Assert.That(new HashSet<ReactiveUI.Primitives.Optional<int>>(results).IsSubsetOf(intList)).IsTrue();
        await Assert.That(new HashSet<ReactiveUI.Primitives.Optional<int>>(intList).IsSubsetOf(results)).IsTrue();
    }

    [Test]
    public async Task OnHasValueInvokesCorrectAction()
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
        await Assert.That(value).IsEqualTo(SomeCount);
        await Assert.That(noValue).IsEqualTo(NoneCount);
    }

    [Test]
    public async Task OnHasNoValueInvokesCorrectAction()
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
        await Assert.That(value).IsEqualTo(SomeCount);
        await Assert.That(noValue).IsEqualTo(NoneCount);
    }

    [Test]
    public async Task SelectValuesReturnsTheValues()
    {
        // having
        var enumerable = OptIntEnum.Concat(OptNoneIntEnum);
        var observable = enumerable.ToObservable().SelectValues();

        // when
        var expected = enumerable.Where(opt => opt.HasValue).Count();
        var results = observable.ToEnumerable().Count();

        // then
        await Assert.That(expected).IsEqualTo(results);
        await Assert.That(results).IsEqualTo(SomeCount);
    }

    [Test]
    public async Task ValueOrInvokesSelector()
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
        await Assert.That(expected).IsEqualTo(results);
        await Assert.That(results).IsEqualTo(NoneCount);
        await Assert.That(invokeCount).IsEqualTo(NoneCount);
    }

    [Test]
    public async Task ValueOrDefaultReturnsDefaultValues()
    {
        // having
        var enumerable = OptStringWithNoneEnum;
        var observable = enumerable.ToObservable().ValueOrDefault();

        // when
        var expected = enumerable.Where(opt => !opt.HasValue).Count();
        var results = observable.ToEnumerable().Where(str => str == default).Count();

        // then
        await Assert.That(expected).IsEqualTo(results);
        await Assert.That(results).IsEqualTo(NoneCount);
    }

    [Test]
    public async Task ValueOrThrowFailsWithGeneratedError()
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
        await Assert.That(receivedError).IsEqualTo(expectedError);
    }

    private static ReactiveUI.Primitives.Optional<int> ParseIntOpt(string input) =>
        int.TryParse(input, out var result) ? ReactiveUI.Primitives.Optional<int>.Some(result) : ReactiveUI.Primitives.Optional<int>.None;

    private static int ParseInt(string input) => int.Parse(input);
}
