namespace Leontes.Infrastructure.Telegram;

public sealed class TelegramRateLimitedException(int retryAfterSeconds, string method)
    : Exception($"Telegram rate limited on {method}, retry after {retryAfterSeconds}s")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
