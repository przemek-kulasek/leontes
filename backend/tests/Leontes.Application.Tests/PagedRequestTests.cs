namespace Leontes.Application.Tests;

public sealed class PagedRequestTests
{
    [Fact]
    public void Constructor_WithDefaults_HasExpectedValues()
    {
        var request = new PagedRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Null(request.SortBy);
        Assert.Null(request.SortDirection);
    }

    [Fact]
    public void Constructor_WithNegativePage_ClampedToOne()
    {
        var request = new PagedRequest(Page: -5);

        Assert.Equal(1, request.Page);
    }

    [Fact]
    public void Constructor_WithExcessivePageSize_ClampedToHundred()
    {
        var request = new PagedRequest(PageSize: 500);

        Assert.Equal(100, request.PageSize);
    }

    [Fact]
    public void Constructor_WithZeroPageSize_ClampedToOne()
    {
        var request = new PagedRequest(PageSize: 0);

        Assert.Equal(1, request.PageSize);
    }

    [Fact]
    public void Skip_CalculatesCorrectOffset()
    {
        var request = new PagedRequest(Page: 3, PageSize: 10);

        Assert.Equal(20, request.Skip);
    }
}
