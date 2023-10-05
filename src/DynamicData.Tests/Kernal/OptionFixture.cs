using System;
using System.Globalization;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Kernal;

public class OptionFixture
{
    [Fact]
    public void ImplictCastHasValue()
    {
        var person = new Person("Name", 20);
        Optional<Person> option = person;

        option.HasValue.Should().BeTrue();
        ReferenceEquals(person, option.Value).Should().BeTrue();
    }

    [Fact]
    public void OptionElseInvokedIfOptionHasNoValue()
    {
        Optional<Person>? source = null;

        var ifactioninvoked = false;
        var elseactioninvoked = false;

        source.IfHasValue(p => ifactioninvoked = true).Else(() => elseactioninvoked = true);

        ifactioninvoked.Should().BeFalse();
        elseactioninvoked.Should().BeTrue();
    }

    [Fact]
    public void OptionIfHasValueInvokedIfOptionHasValue()
    {
        Optional<Person> source = new Person("A", 1);

        var ifactioninvoked = false;
        var elseactioninvoked = false;

        source.IfHasValue(p => ifactioninvoked = true).Else(() => elseactioninvoked = true);

        ifactioninvoked.Should().BeTrue();
        elseactioninvoked.Should().BeFalse();
    }

    [Fact]
    public void OptionNoneHasNoValue()
    {
        var option = Optional.None<IChangeSet<Person, string>>();
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void OptionSetToNullHasNoValue1()
    {
        Person? person = null;
        var option = Optional.Some(person);
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void OptionSetToNullHasNoValue2()
    {
        Person? person = null;
        Optional<Person> option = person;
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void OptionSomeHasValue()
    {
        var person = new Person("Name", 20);
        var option = Optional.Some(person);
        option.HasValue.Should().BeTrue();
        ReferenceEquals(person, option.Value).Should().BeTrue();
    }

    [Fact]
    public void OptionConvertThrowsIfConverterIsNull()
    {
        var caught = false;

        Func<string, string>? converter = null;

        try
        {
            Optional.None<string>().Convert(converter!);
        }
        catch (ArgumentNullException)
        {
            caught = true;
        }

        caught.Should().BeTrue();
    }

    [Fact]
    public void OptionConvertToOptionalInvokesConverterWithValue()
    {
        var option = Optional.Some(string.Empty);
        var invoked = false;

        Optional<string> Converter(string input)
        {
            invoked = true;
            return Optional.Some(input);
        }

        var result = option.Convert(Converter);

        invoked.Should().BeTrue();
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void OptionConvertToOptionalInvokesConverterOnlyWithValue()
    {
        var option = Optional.None<string>();
        var invoked = false;

        Optional<string> Converter(string input)
        {
            invoked = true;
            return Optional.Some(input);
        }

        var result = option.Convert(Converter);

        invoked.Should().BeFalse();
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void OptionConvertToOptionalCanReturnValue()
    {
        const int TestData = 37;

        var option = Optional.Some(TestData.ToString());

        var result = option.Convert(ParseInt);

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(TestData);
    }

    [Fact]
    public void OptionConvertToOptionalCanReturnNone()
    {
        var option = Optional.Some("Not An Int");

        var result = option.Convert(ParseInt);

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void OptionConvertToOptionalThrowsIfConverterIsNull()
    {
        var caught = false;

        Func<string, Optional<string>>? converter = null;

        try
        {
            Optional.None<string>().Convert(converter!);
        }
        catch (ArgumentNullException)
        {
            caught = true;
        }

        caught.Should().BeTrue();
    }

    [Fact]
    public void OptionOrElseInvokesWithoutValue()
    {
        var option = Optional.None<string>();
        var invoked = false;

        Optional<string> Fallback()
        {
            invoked = true;
            return Optional.None<string>();
        }

        var result = option.OrElse(Fallback);

        invoked.Should().BeTrue();
    }

    [Fact]
    public void OptionOrElseInvokesOnlyWithoutValue()
    {
        var option = Optional.Some(string.Empty);
        var invoked = false;

        Optional<string> Fallback()
        {
            invoked = true;
            return Optional.None<string>();
        }

        var result = option.OrElse(Fallback);

        invoked.Should().BeFalse();
    }

    [Fact]
    public void OptionOrElseCanReturnValue()
    {
        const string TestString = nameof(TestString);

        var option = Optional.None<string>();
        var result = option.OrElse(() => TestString);

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(TestString);
    }

    [Fact]
    public void OptionOrElseCanReturnNone()
    {
        var option = Optional.None<string>();
        var result = option.OrElse(Optional.None<string>);

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void OptionOrElseCanBeChained()
    {
        const int Expected = unchecked((int)0xc001d00d);

        var option = Optional.None<string>();
        var result = option.OrElse(Optional.None<string>)
                                      .OrElse(() => Optional.Some(Expected.ToString("x")))
                                      .Convert(s => ParseInt(s).OrElse(() => ParseHex(s)));

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(Expected);
    }

    [Fact]
    public void OptionOrElseThrowsIfFallbackIsNull()
    {
        var caught = false;

        try
        {
            Optional.None<string>().OrElse(null!);
        }
        catch(ArgumentNullException)
        {
            caught = true;
        }

        caught.Should().BeTrue();
    }

    private static Optional<int> ParseInt(string input) =>
        int.TryParse(input, out var result) ? Optional.Some(result) : Optional.None<int>();

    private static Optional<int> ParseHex(string input) =>
        int.TryParse(input, NumberStyles.HexNumber, null, out var result) ? Optional.Some(result) : Optional.None<int>();
}
