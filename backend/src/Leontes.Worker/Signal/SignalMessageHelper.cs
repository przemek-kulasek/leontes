using Leontes.Application.Messaging;
using Leontes.Worker.Messaging;

namespace Leontes.Worker.Signal;

public static class SignalMessageHelper
{
    public const int MaxSignalMessageLength = 2000;

    public static List<string> SplitMessage(string text)
    {
        return MessageSplitter.Split(text, MaxSignalMessageLength).ToList();
    }

    public static Task<string> ReadSseResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return SseResponseReader.ReadSseResponseAsync(response, cancellationToken);
    }
}
