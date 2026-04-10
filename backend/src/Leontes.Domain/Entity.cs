namespace Leontes.Domain;

public abstract class Entity
{
    public Guid Id { get; init; }
    public DateTime Created { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
