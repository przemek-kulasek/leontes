namespace Leontes.Infrastructure.Telegram;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public int PollTimeoutSeconds { get; set; } = 30;
    public List<long> AllowedChatIds { get; set; } = [];
}
