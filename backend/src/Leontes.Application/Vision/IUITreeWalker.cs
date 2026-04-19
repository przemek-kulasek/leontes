using Leontes.Domain.Vision;

namespace Leontes.Application.Vision;

public interface IUITreeWalker
{
    Task<UIElement?> CaptureWindowTreeAsync(
        int processId,
        TreeWalkerOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<UIElement?> CaptureFocusedWindowTreeAsync(
        TreeWalkerOptions? options = null,
        CancellationToken cancellationToken = default);
}
