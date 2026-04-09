namespace Leontes.Application.Tests;

public sealed class PagedResponseTests
{
    [Fact]
    public void TotalPages_CalculatesCorrectly_WhenExactDivision()
    {
        var response = new PagedResponse<string>([], Page: 1, PageSize: 10, TotalCount: 30);

        Assert.Equal(3, response.TotalPages);
    }

    [Fact]
    public void TotalPages_RoundsUp_WhenPartialPage()
    {
        var response = new PagedResponse<string>([], Page: 1, PageSize: 10, TotalCount: 25);

        Assert.Equal(3, response.TotalPages);
    }

    [Fact]
    public void TotalPages_ReturnsZero_WhenNoItems()
    {
        var response = new PagedResponse<string>([], Page: 1, PageSize: 10, TotalCount: 0);

        Assert.Equal(0, response.TotalPages);
    }

    [Fact]
    public void TotalPages_ReturnsOne_WhenFewerItemsThanPageSize()
    {
        var response = new PagedResponse<string>(["a", "b"], Page: 1, PageSize: 10, TotalCount: 2);

        Assert.Equal(1, response.TotalPages);
    }
}
