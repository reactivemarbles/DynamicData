namespace DynamicData.Tests.Domain
{
    public class ParentChild
    {
        public ParentChild(Person child, Person parent)
        {
            Child = child;
            Parent = parent;
        }

        public Person Child { get; }

        public Person Parent { get; }
    }
}