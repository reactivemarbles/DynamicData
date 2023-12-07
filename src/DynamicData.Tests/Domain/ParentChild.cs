namespace DynamicData.Tests.Domain;

public class ParentChild(Person child, Person parent)
{
    public Person Child { get; } = child;

    public Person Parent { get; } = parent;
}
