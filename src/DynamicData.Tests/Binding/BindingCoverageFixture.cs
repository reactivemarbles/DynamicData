#if REACTIVE_TESTS
using DynamicData.Reactive;
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using System.Linq.Expressions;
using System.Reflection;
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public sealed class BindingCoverageFixture
{
    [Test]
    public async Task WhenChangedCombinesTwoPropertiesAndUsesFallback()
    {
        var source = new NotifyingNode { A = 1, B = 2 };

        using var subscription = source.WhenChanged(
                node => node.Child!.A,
                node => node.B,
                static (_, childA, b) => $"{childA}:{b}",
                static () => -1)
            .RecordValues(out var results);

        await Assert.That(results.RecordedValues).IsEquivalentTo(new string?[] { "-1:2" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var child = new NotifyingNode { A = 10 };
        source.Child = child;
        child.A = 11;
        source.B = 3;

        await Assert.That(results.RecordedValues).IsEquivalentTo(new string?[] { "-1:2", "10:2", "11:2", "11:3" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task WhenChangedCombinesThreeProperties()
    {
        var source = new NotifyingNode { A = 1, B = 2, C = 3 };

        using var subscription = source.WhenChanged(
                node => node.A,
                node => node.B,
                node => node.C,
                static (_, a, b, c) => a + b + c)
            .RecordValues(out var results);

        source.C = 4;

        await Assert.That(results.RecordedValues).IsEquivalentTo(new[] { 6, 7 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task WhenChangedCombinesFourProperties()
    {
        var source = new NotifyingNode { A = 1, B = 2, C = 3, D = 4 };

        using var subscription = source.WhenChanged(
                node => node.A,
                node => node.B,
                node => node.C,
                node => node.D,
                static (_, a, b, c, d) => a + b + c + d)
            .RecordValues(out var results);

        source.D = 5;

        await Assert.That(results.RecordedValues).IsEquivalentTo(new[] { 10, 11 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task WhenChangedCombinesFiveProperties()
    {
        var source = new NotifyingNode { A = 1, B = 2, C = 3, D = 4, E = 5 };

        using var subscription = source.WhenChanged(
                node => node.A,
                node => node.B,
                node => node.C,
                node => node.D,
                node => node.E,
                static (_, a, b, c, d, e) => a + b + c + d + e)
            .RecordValues(out var results);

        source.E = 6;

        await Assert.That(results.RecordedValues).IsEquivalentTo(new[] { 15, 16 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task WhenChangedCombinesSixProperties()
    {
        var source = new NotifyingNode { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6 };

        using var subscription = source.WhenChanged(
                node => node.A,
                node => node.B,
                node => node.C,
                node => node.D,
                node => node.E,
                node => node.F,
                static (_, a, b, c, d, e, f) => a + b + c + d + e + f)
            .RecordValues(out var results);

        source.F = 7;

        await Assert.That(results.RecordedValues).IsEquivalentTo(new[] { 21, 22 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task WhenChangedRejectsNullArgumentsBeforeSubscribing()
    {
        var source = new NotifyingNode();

        await Assert.That(() => NotifyPropertyChangedEx.WhenChanged<NotifyingNode, int, int, int>(null!, node => node.A, node => node.B, static (_, a, b) => a + b)).Throws<ArgumentNullException>();
        await Assert.That(() => source.WhenChanged<NotifyingNode, int, int, int>(null!, node => node.B, static (_, a, b) => a + b)).Throws<ArgumentNullException>();
        await Assert.That(() => source.WhenChanged<NotifyingNode, int, int, int>(node => node.A, null!, static (_, a, b) => a + b)).Throws<ArgumentNullException>();
        await Assert.That(() => source.WhenChanged<NotifyingNode, int, int, int>(node => node.A, node => node.B, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task BindingListCloneReplaysIndexedAndUnindexedItemChanges()
    {
        var list = new BindingList<string> { "b", "c" };
        var changes = new ChangeSet<string>
        {
            new(ListChangeReason.Add, "a", 0),
            new(ListChangeReason.Add, "d"),
            new(ListChangeReason.Replace, "bb", ReactiveUI.Primitives.Optional<string>.Create("b"), 1, -1),
            new(ListChangeReason.Replace, "ee", ReactiveUI.Primitives.Optional<string>.Create("missing"), -1, -1),
            new(ListChangeReason.Remove, "d"),
            new(ListChangeReason.Remove, "missing"),
            new(ListChangeReason.RemoveRange, new[] { "c" }),
        };

        BindingListEx.Clone(list, changes);

        await Assert.That(list).IsEquivalentTo(new[] { "a", "bb", "ee" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task BindingListCloneReplaysRangeClearRefreshAndMoveChanges()
    {
        var list = new BindingList<string> { "a", "b", "c" };
        var notifications = new List<ListChangedType>();
        list.ListChanged += (_, args) => notifications.Add(args.ListChangedType);

        BindingListEx.Clone(
            list,
            new ChangeSet<string> { new(ListChangeReason.AddRange, new[] { "d", "e" }, 1) });

        await Assert.That(list).IsEquivalentTo(new[] { "a", "d", "e", "b", "c" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        BindingListEx.Clone(
            list,
            new ChangeSet<string> { new(ListChangeReason.Refresh, "a", 0) });

        await Assert.That(list).IsEquivalentTo(new[] { "a", "d", "e", "b", "c" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notifications).Contains(ListChangedType.ItemChanged);

        BindingListEx.Clone(
            list,
            new ChangeSet<string> { new("c", 0, 4) });

        await Assert.That(list).IsEquivalentTo(new[] { "c", "a", "d", "e", "b" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        BindingListEx.Clone(
            list,
            new ChangeSet<string> { new(ListChangeReason.Clear, new[] { "c", "a", "d", "e", "b" }) });

        await Assert.That(list).IsEmpty();
        await Assert.That(notifications).Contains(ListChangedType.ItemDeleted);
        await Assert.That(notifications).Contains(ListChangedType.Reset);

        BindingListEx.Clone(
            list,
            new ChangeSet<string> { new(ListChangeReason.Refresh, "missing", 0) });

        await Assert.That(list).IsEmpty();
    }

    [Test]
    public async Task BindingListCloneThrowsWhenMoveHasNoIndex()
    {
        var list = new BindingList<string> { "a" };
        var changes = new ChangeSet<string> { new(ListChangeReason.Moved, "a") };

        await Assert.That(() => BindingListEx.Clone(list, changes)).Throws<UnspecifiedIndexException>();
    }

    [Test]
    public async Task BindingListKeyedObservableChangeSetTracksAddsAndRefreshes()
    {
        var list = new BindingList<Person>();

        using var results = list.ToObservableChangeSet(person => person.Name).AsAggregator();
        list.Add(new Person("A", 1));
        list.Add(new Person("B", 2));
        list.ResetBindings();

        await Assert.That(results.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Lookup("A").HasValue).IsTrue();
        await Assert.That(results.Messages.Last().Removes).IsEqualTo(2);
        await Assert.That(results.Messages.Last().Adds).IsEqualTo(2);
    }

    [Test]
    public async Task ExpressionBuilderCreatesInvokerForSupportedExpressions()
    {
        var source = new ExpressionSubject { Name = "Alpha" };
        var propertyInvoker = Expression.Property(Expression.Parameter(typeof(ExpressionSubject), "source"), nameof(ExpressionSubject.Name)).CreateInvoker();
        var convertInvoker = Expression.Convert(Expression.Parameter(typeof(ExpressionSubject), "source"), typeof(object)).CreateInvoker();

        await Assert.That(propertyInvoker(source)).IsEqualTo("Alpha");
        await Assert.That(convertInvoker(source)).IsEqualTo(source);
    }

    [Test]
    public async Task ExpressionBuilderInvokerRejectsUnsupportedMembersAndNodes()
    {
        var parameter = Expression.Parameter(typeof(ExpressionSubject), "source");
        var field = Expression.Field(parameter, nameof(ExpressionSubject.Field));
        var staticProperty = Expression.Property(null, typeof(ExpressionSubject).GetProperty(nameof(ExpressionSubject.StaticName))!);
        Expression? nullExpression = null;

        await Assert.That(() => field.CreateInvoker()).Throws<ArgumentException>();
        await Assert.That(() => Expression.Property(parameter, typeof(ExpressionSubject).GetProperty(nameof(ExpressionSubject.WriteOnly))!).CreateInvoker()).Throws<ArgumentException>();
        await Assert.That(() => staticProperty.CreateInvoker()).Throws<ArgumentException>();
        await Assert.That(() => nullExpression!.CreateInvoker()).Throws<ArgumentNullException>();
        await Assert.That(() => Expression.Constant(1).CreateInvoker()).Throws<ArgumentException>();
    }

    [Test]
    public async Task ExpressionBuilderSplitsAndRejectsExpressionSteps()
    {
        Expression<Func<ExpressionSubject, object>> convertedProperty = subject => subject.Number;
        Expression<Func<ExpressionSubject, string>> methodCall = subject => subject.Name.ToString();

        var steps = convertedProperty.SplitIntoSteps().ToArray();

        await Assert.That(steps.Length).IsEqualTo(2);
        await Assert.That(steps[0].NodeType).IsEqualTo(ExpressionType.Convert);
        await Assert.That(steps[1].NodeType).IsEqualTo(ExpressionType.MemberAccess);
        await Assert.That(() => methodCall.SplitIntoSteps().ToArray()).Throws<ArgumentException>();
    }

    [Test]
    public async Task ExpressionBuilderGetsPropertiesMembersAndCacheKeys()
    {
        Expression<Func<ExpressionSubject, object>> convertedProperty = subject => subject.Number;
        Expression<Func<ExpressionSubject, string>> fieldExpression = subject => subject.Field;
        Expression<Func<ExpressionSubject, string>> methodCall = subject => subject.Name.ToString();
        Expression<Func<ExpressionSubject, int>> nestedProperty = subject => subject.Child!.Number;

        await Assert.That(convertedProperty.GetProperty().Name).IsEqualTo(nameof(ExpressionSubject.Number));
        await Assert.That(methodCall.GetMember()).IsTypeOf<MethodInfo>();
        await Assert.That(() => fieldExpression.GetProperty()).Throws<ArgumentException>();
        await Assert.That(() => methodCall.GetProperty()).Throws<ArgumentException>();
        await Assert.That(nestedProperty.ToCacheKey()).Contains($"{nameof(ExpressionSubject.Child)}.{nameof(ExpressionSubject.Number)}");
    }

    [Test]
    public async Task ExpressionBuilderMemberPropertyRejectsFields()
    {
        var parameter = Expression.Parameter(typeof(ExpressionSubject), "source");
        var field = Expression.Field(parameter, nameof(ExpressionSubject.Field));

        await Assert.That(() => field.GetProperty()).Throws<ArgumentException>();
    }

    [Test]
    public async Task ExpressionBuilderPropertyChangedFactoryObservesOnlyMatchingProperty()
    {
        Expression<Func<ExpressionSubject, string>> expression = subject => subject.Name;
        var subject = new ExpressionSubject { Name = "Alpha", Number = 1 };
        var hits = 0;

        using var subscription = expression.Body.CreatePropertyChangedFactory()(subject).Subscribe(_ => hits++);
        subject.Number = 2;
        subject.Name = "Beta";

        await Assert.That(hits).IsEqualTo(1);
    }

    [Test]
    public async Task ExpressionBuilderPropertyChangedFactoryReturnsNeverForNonNotifyProperty()
    {
        var parameter = Expression.Parameter(typeof(PlainSubject), "source");
        var expression = Expression.Property(parameter, nameof(PlainSubject.Name));
        var subject = new PlainSubject { Name = "Alpha" };
        var hits = 0;

        using var subscription = expression.CreatePropertyChangedFactory()(subject).Subscribe(_ => hits++);
        subject.Name = "Beta";

        await Assert.That(hits).IsEqualTo(0);
    }

    private sealed class NotifyingNode : INotifyPropertyChanged
    {
        private int _a;
        private int _b;
        private int _c;
        private int _d;
        private int _e;
        private int _f;
        private NotifyingNode? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int A
        {
            get => _a;
            set => SetProperty(ref _a, value, nameof(A));
        }

        public int B
        {
            get => _b;
            set => SetProperty(ref _b, value, nameof(B));
        }

        public int C
        {
            get => _c;
            set => SetProperty(ref _c, value, nameof(C));
        }

        public int D
        {
            get => _d;
            set => SetProperty(ref _d, value, nameof(D));
        }

        public int E
        {
            get => _e;
            set => SetProperty(ref _e, value, nameof(E));
        }

        public int F
        {
            get => _f;
            set => SetProperty(ref _f, value, nameof(F));
        }

        public NotifyingNode? Child
        {
            get => _child;
            set => SetProperty(ref _child, value, nameof(Child));
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class ExpressionSubject : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _number;
        private ExpressionSubject? _child;

        public string Field = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static string StaticName => "Static";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, nameof(Name));
        }

        public int Number
        {
            get => _number;
            set => SetProperty(ref _number, value, nameof(Number));
        }

        public ExpressionSubject? Child
        {
            get => _child;
            set => SetProperty(ref _child, value, nameof(Child));
        }

        public int WriteOnly
        {
            set => _number = value;
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class PlainSubject
    {
        public string Name { get; set; } = string.Empty;
    }
}
