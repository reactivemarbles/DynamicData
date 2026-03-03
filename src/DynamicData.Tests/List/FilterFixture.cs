namespace DynamicData.Tests.List;

public static partial class FilterFixture
{
    public enum SourceType
    {
        Immediate,
        Asynchronous
    }

    public enum EmptyChangesetPolicy
    {
        SuppressEmptyChangesets,
        IncludeEmptyChangesets
    }

    public record Item
    {
        public static bool FilterByEvenId(Item item)
            => (item.Id % 2) == 0;

        public static bool FilterByIsIncluded(Item item)
            => item.IsIncluded;

        public static bool FilterByIdInclusionMask(
                int     idInclusionMask,
                Item    item)
            => ((item.Id & idInclusionMask) == 0) && item.IsIncluded;

        public required int Id { get; init; }

        public bool IsIncluded { get; set; }
    }
}
