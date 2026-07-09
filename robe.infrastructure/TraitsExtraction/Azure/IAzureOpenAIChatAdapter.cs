namespace Robe.Infrastructure.TraitsExtraction.Azure;

/// <summary>
/// Seam that isolates the raw Azure OpenAI HTTP call so unit tests can
/// supply fake responses without starting a real HTTP stack.
/// </summary>
internal interface IAzureOpenAIChatAdapter
{
    Task<ChatAdapterResponse> CompleteChatAsync(
        ChatAdapterRequest request,
        CancellationToken ct);
}

internal sealed record ChatAdapterRequest(
    string SystemPrompt,
    string UserInstruction,
    IReadOnlyList<ChatImageEntry> Images);

internal sealed record ChatImageEntry(string ImageId, byte[] Content, string ContentType);

internal sealed record ChatAdapterResponse(string? Content, bool IsContentFiltered);
