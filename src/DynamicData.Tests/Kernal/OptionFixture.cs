using System.Globalization;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Kernal;

public class OptionFixture
{
    [Test]
    public async Task ImplictCastHasValue()
    {
        var person = new Person("Name", 20);
        ReactiveUI.Primitives.Optional<Person> option = person;

        await Assert.That(option.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(person, option.Value)).IsTrue();
    }

    [Test]
    public async Task OptionElseInvokedIfOptionHasNoValue()
    {
        ReactiveUI.Primitives.Optional<Person>? source = null;

        var ifactioninvoked = false;
        var elseactioninvoked = false;

        source.IfHasValue(p => ifactioninvoked = true).Else(() => elseactioninvoked = true);

        await Assert.That(ifactioninvoked).IsFalse();
        await Assert.That(elseactioninvoked).IsTrue();
    }

    [Test]
    public async Task OptionIfHasValueInvokedIfOptionHasValue()
    {
        ReactiveUI.Primitives.Optional<Person> source = new Person("A", 1);

        var ifactioninvoked = false;
        var elseactioninvoked = false;

        source.IfHasValue(p => ifactioninvoked = true).Else(() => elseactioninvoked = true);

        await Assert.That(ifactioninvoked).IsTrue();
        await Assert.That(elseactioninvoked).IsFalse();
    }

    [Test]
    public async Task OptionNoneHasNoValue()
    {
        var option = ReactiveUI.Primitives.Optional<IChangeSet<Person, string>>.None;
        await Assert.That(option.HasValue).IsFalse();
    }

    [Test]
    public async Task OptionSetToNullHasNoValue1()
    {
        Person person = default!;
        var option = ReactiveUI.Primitives.Optional<Person>.Some(person);
        await Assert.That(option.HasValue).IsFalse();
    }

    [Test]
    public async Task OptionSetToNullHasNoValue2()
    {
        Person person = default!;
        ReactiveUI.Primitives.Optional<Person> option = person;
        await Assert.That(option.HasValue).IsFalse();
    }

    [Test]
    public async Task OptionSomeHasValue()
    {
        var person = new Person("Name", 20);
        var option = ReactiveUI.Primitives.Optional<Person>.Some(person);
        await Assert.That(option.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(person, option.Value)).IsTrue();
    }

    [Test]
    public async Task OptionConvertThrowsIfConverterIsNull()
    {
        var caught = false;

        Func<string, string>? converter = null;

        try
        {
            ReactiveUI.Primitives.Optional<string>.None.Convert(converter!);
        }
        catch (ArgumentNullException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    [Test]
    public async Task OptionConvertToOptionalInvokesConverterWithValue()
    {
        var option = ReactiveUI.Primitives.Optional<string>.Some(string.Empty);
        var invoked = false;

        ReactiveUI.Primitives.Optional<string> Converter(string input)
        {
            invoked = true;
            return ReactiveUI.Primitives.Optional<string>.Some(input);
        }

        var result = option.Convert(Converter);

        await Assert.That(invoked).IsTrue();
        await Assert.That(result.HasValue).IsTrue();
    }

    [Test]
    public async Task OptionConvertToOptionalInvokesConverterOnlyWithValue()
    {
        var option = ReactiveUI.Primitives.Optional<string>.None;
        var invoked = false;

        ReactiveUI.Primitives.Optional<string> Converter(string input)
        {
            invoked = true;
            return ReactiveUI.Primitives.Optional<string>.Some(input);
        }

        var result = option.Convert(Converter);

        await Assert.That(invoked).IsFalse();
        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task OptionConvertToOptionalCanReturnValue()
    {
        const int TestData = 37;

        var option = ReactiveUI.Primitives.Optional<string>.Some(TestData.ToString());

        var result = option.Convert(ParseInt);

        await Assert.That(result.HasValue).IsTrue();
        await Assert.That(result.Value).IsEqualTo(TestData);
    }

    [Test]
    public async Task OptionConvertToOptionalCanReturnNone()
    {
        var option = ReactiveUI.Primitives.Optional<string>.Some("Not An Int");

        var result = option.Convert(ParseInt);

        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task OptionConvertToOptionalThrowsIfConverterIsNull()
    {
        var caught = false;

        Func<string, ReactiveUI.Primitives.Optional<string>>? converter = null;

        try
        {
            ReactiveUI.Primitives.Optional<string>.None.Convert(converter!);
        }
        catch (ArgumentNullException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    [Test]
    public async Task OptionOrElseInvokesWithoutValue()
    {
        var option = ReactiveUI.Primitives.Optional<string>.None;
        var invoked = false;

        ReactiveUI.Primitives.Optional<string> Fallback()
        {
            invoked = true;
            return ReactiveUI.Primitives.Optional<string>.None;
        }

        var result = option.OrElse(Fallback);

        await Assert.That(invoked).IsTrue();
    }

    [Test]
    public async Task OptionOrElseInvokesOnlyWithoutValue()
    {
        var option = ReactiveUI.Primitives.Optional<string>.Some(string.Empty);
        var invoked = false;

        ReactiveUI.Primitives.Optional<string> Fallback()
        {
            invoked = true;
            return ReactiveUI.Primitives.Optional<string>.None;
        }

        var result = option.OrElse(Fallback);

        await Assert.That(invoked).IsFalse();
    }

    [Test]
    public async Task OptionOrElseCanReturnValue()
    {
        const string TestString = nameof(TestString);

        var option = ReactiveUI.Primitives.Optional<string>.None;
        var result = option.OrElse(() => TestString);

        await Assert.That(result.HasValue).IsTrue();
        await Assert.That(result.Value).IsEqualTo(TestString);
    }

    [Test]
    public async Task OptionOrElseCanReturnNone()
    {
        var option = ReactiveUI.Primitives.Optional<string>.None;
        var result = option.OrElse(() => ReactiveUI.Primitives.Optional<string>.None);

        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task OptionOrElseCanBeChained()
    {
        const int Expected = unchecked((int)0xc001d00d);

        var option = ReactiveUI.Primitives.Optional<string>.None;
        var result = option.OrElse(() => ReactiveUI.Primitives.Optional<string>.None)
                                      .OrElse(() => ReactiveUI.Primitives.Optional<string>.Some(Expected.ToString("x")))
                                      .Convert(s => ParseInt(s).OrElse(() => ParseHex(s)));

        await Assert.That(result.HasValue).IsTrue();
        await Assert.That(result.Value).IsEqualTo(Expected);
    }

    [Test]
    public async Task OptionOrElseThrowsIfFallbackIsNull()
    {
        var caught = false;

        try
        {
            ReactiveUI.Primitives.Optional<string>.None.OrElse(null!);
        }
        catch (ArgumentNullException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    private static ReactiveUI.Primitives.Optional<int> ParseInt(string input) =>
        int.TryParse(input, out var result) ? ReactiveUI.Primitives.Optional<int>.Some(result) : ReactiveUI.Primitives.Optional<int>.None;

    private static ReactiveUI.Primitives.Optional<int> ParseHex(string input) =>
        int.TryParse(input, NumberStyles.HexNumber, null, out var result) ? ReactiveUI.Primitives.Optional<int>.Some(result) : ReactiveUI.Primitives.Optional<int>.None;
}
