using Cardscape.Application.Common;

namespace Cardscape.UnitTests.Application.Common;

public sealed class OffsetPaginationTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1, 0)]
    [InlineData(25, 25)]
    public void NormalizeSkip_Input_ReturnsNonNegativeOffset(int? input, int expected) =>
        OffsetPagination.NormalizeSkip(input).Should().Be(expected);

    [Theory]
    [InlineData(null, OffsetPagination.DefaultTake)]
    [InlineData(0, OffsetPagination.DefaultTake)]
    [InlineData(-1, OffsetPagination.DefaultTake)]
    [InlineData(25, 25)]
    [InlineData(OffsetPagination.MaxTake + 1, OffsetPagination.MaxTake)]
    public void NormalizeTake_Input_ReturnsBoundedPageSize(int? input, int expected) =>
        OffsetPagination.NormalizeTake(input).Should().Be(expected);
}
