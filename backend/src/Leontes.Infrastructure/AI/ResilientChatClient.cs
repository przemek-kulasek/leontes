using System.Runtime.CompilerServices;
using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI;

/// <summary>
/// Wraps an <see cref="IChatClient"/> with timeout, retry, and failure tracking
/// per feature 85 — Error Recovery and Resilience.
/// </summary>
public sealed class ResilientChatClient(
    IChatClient inner,
    ILlmAvailability availability,
    IOptions<ResilienceOptions> options,
    ILogger<ResilientChatClient> logger) : DelegatingChatClient(inner)
{
    private readonly LlmResilienceOptions _options = options.Value.Llm;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var baseTimeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeoutForAttempt(baseTimeout, attempt));

                var response = await base.GetResponseAsync(messages, options, cts.Token);
                availability.RecordSuccess();
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                if (!ShouldRetry(attempt))
                {
                    availability.RecordFailure();
                    logger.LogError(ex, "LLM call timed out after {Attempts} attempts", attempt);
                    throw;
                }
                logger.LogWarning(ex, "LLM call timed out (attempt {Attempt}); retrying", attempt);
            }
            catch (Exception ex) when (IsAuthError(ex))
            {
                availability.RecordFailure();
                logger.LogError(ex, "LLM authentication failed; not retrying");
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (!ShouldRetry(attempt))
                {
                    availability.RecordFailure();
                    logger.LogError(ex, "LLM call failed after {Attempts} attempts", attempt);
                    throw;
                }
                logger.LogWarning(ex, "Transient LLM failure (attempt {Attempt}); backing off", attempt);
                await Task.Delay(BackoffFor(attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                availability.RecordFailure();
                logger.LogError(ex, "LLM call failed with non-transient error");
                throw;
            }
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var baseTimeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(baseTimeout);

        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        var sawChunk = false;

        try
        {
            enumerator = base.GetStreamingResponseAsync(messages, options, cts.Token).GetAsyncEnumerator(cts.Token);
        }
        catch (Exception ex)
        {
            availability.RecordFailure();
            logger.LogError(ex, "Failed to open LLM streaming response");
            throw;
        }

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    availability.RecordFailure();
                    logger.LogWarning(ex,
                        "LLM streaming interrupted after {ChunkSeen} (sawChunk={SawChunk})",
                        sawChunk ? "partial delivery" : "no chunks", sawChunk);
                    yield break;
                }

                if (!hasNext)
                {
                    break;
                }

                sawChunk = true;
                yield return enumerator.Current;
            }

            availability.RecordSuccess();
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }

    private bool ShouldRetry(int attempt) => attempt <= Math.Max(0, _options.MaxRetries);

    private TimeSpan BackoffFor(int attempt)
    {
        // 1s, 4s, 16s using base delay and 4x exponent
        var baseDelay = Math.Max(1, _options.RetryBaseDelaySeconds);
        var seconds = baseDelay * Math.Pow(4, attempt - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, 60));
    }

    private static TimeSpan TimeoutForAttempt(TimeSpan baseTimeout, int attempt) =>
        attempt == 1 ? baseTimeout : TimeSpan.FromMilliseconds(baseTimeout.TotalMilliseconds * 1.5);

    private static bool IsAuthError(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("401", StringComparison.Ordinal)
            || msg.Contains("403", StringComparison.Ordinal)
            || msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Forbidden", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is HttpRequestException) return true;
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("429", StringComparison.Ordinal)
            || msg.Contains("503", StringComparison.Ordinal)
            || msg.Contains("502", StringComparison.Ordinal)
            || msg.Contains("504", StringComparison.Ordinal)
            || msg.Contains("500", StringComparison.Ordinal);
    }
}
