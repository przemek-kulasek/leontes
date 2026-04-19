using Leontes.Domain.Vision;

namespace Leontes.Application.Vision;

public interface ITreeSerializer
{
    string Serialize(UIElement root, TreeSerializerOptions? options = null);
}
