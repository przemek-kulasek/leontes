using Leontes.Domain;

namespace Leontes.Domain.Tests;

public sealed class EntityTests
{
    [Fact]
    public void NewEntity_HasDefaultValues()
    {
        var entity = new TestEntity();

        Assert.Equal(Guid.Empty, entity.Id);
        Assert.Equal(default, entity.Created);
        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.LastModified);
        Assert.Null(entity.LastModifiedBy);
    }

    private sealed class TestEntity : Entity;
}
