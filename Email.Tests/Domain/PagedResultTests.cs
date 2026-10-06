using Email.Domain.Models;
using Xunit;

namespace Email.Tests.Domain;

public class PagedResultTests
{
    [Fact]
    public void EmptyResult_StillHasOnePage()
    {
        var page = PagedResult<string>.Empty(page: 1, pageSize: 20);

        Assert.Equal(0, page.Total);
        Assert.Equal(1, page.TotalPages);
        Assert.False(page.HasNext);
        Assert.False(page.HasPrevious);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(100, 5)]
    public void TotalPages_RoundsUp(int total, int expectedPages)
    {
        var page = new PagedResult<string> { Total = total, PageSize = 20, Page = 1 };

        Assert.Equal(expectedPages, page.TotalPages);
    }

    [Fact]
    public void HasNextAndHasPrevious_ReflectCurrentPage()
    {
        var second = new PagedResult<string> { Total = 45, PageSize = 20, Page = 2 };
        var last = new PagedResult<string> { Total = 45, PageSize = 20, Page = 3 };

        Assert.True(second.HasNext);
        Assert.True(second.HasPrevious);
        Assert.False(last.HasNext);
        Assert.True(last.HasPrevious);
    }

    [Fact]
    public void Filter_NormalizesPageAndSize()
    {
        var filter = new EmailQueryFilter { Page = 0, PageSize = 0 };
        Assert.Equal(1, filter.NormalizedPage);
        Assert.Equal(1, filter.NormalizedPageSize);
        Assert.Equal(0, filter.Skip);

        var tooBig = new EmailQueryFilter { Page = 3, PageSize = 1000 };
        Assert.Equal(EmailQueryFilter.MaxPageSize, tooBig.NormalizedPageSize);
        Assert.Equal(2 * EmailQueryFilter.MaxPageSize, tooBig.Skip);
    }
}
