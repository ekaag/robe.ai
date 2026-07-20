using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.TraitsExtraction.Azure;

/// <summary>
/// Production adapter: builds a <see cref="ChatClient"/> authenticated via
/// <see cref="DefaultAzureCredential"/> (Managed Identity on App Service;
/// Azure CLI / VS credentials locally) and forwards the request to the
/// Azure OpenAI Chat Completions API.
///
/// The endpoint and deployment name are fetched from <see cref="ISecretManager"/>
/// (Key Vault in production) on first use and cached for the lifetime of this singleton.
///
/// The Azure SDK's built-in retry pipeline handles transient HTTP 429/408/5xx
/// failures with exponential backoff — no additional retry layer is added here
/// to avoid nested retry amplification.
/// </summary>
internal sealed class AzureOpenAIChatAdapter : IAzureOpenAIChatAdapter
{
    private readonly ISecretManager _secrets;
    private ChatClient? _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AzureOpenAIChatAdapter(ISecretManager secrets)
    {
        _secrets = secrets;
    }

    private async Task<ChatClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_client is not null) return _client;
            var endpoint = (await _secrets.GetSecretAsync("AzureOpenAI:Endpoint", ct)).Trim();
            var deployment = (await _secrets.GetSecretAsync("AzureOpenAI:DeploymentName", ct)).Trim();
            var azure = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
            _client = azure.GetChatClient(deployment);
            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<ChatAdapterResponse> CompleteChatAsync(
        ChatAdapterRequest request,
        CancellationToken ct)
    {
        var userParts = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(request.UserInstruction)
        };

        foreach (var img in request.Images)
        {
            userParts.Add(ChatMessageContentPart.CreateTextPart($"[Image: {img.ImageId}]"));
            userParts.Add(ChatMessageContentPart.CreateImagePart(
                BinaryData.FromBytes(img.Content), img.ContentType, ParseImageDetailLevel(request.ImageDetailLevel)));
        }

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(request.SystemPrompt),
            new UserChatMessage(userParts)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var client = await GetClientAsync(ct);
        ChatCompletion completion = await client.CompleteChatAsync(messages, options, ct);

        bool isFiltered = completion.FinishReason == ChatFinishReason.ContentFilter;
        string? content = completion.Content.Count > 0 ? completion.Content[0].Text : null;
        return new ChatAdapterResponse(content, isFiltered);
    }

    private static ChatImageDetailLevel ParseImageDetailLevel(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "low" => ChatImageDetailLevel.Low,
            "auto" => ChatImageDetailLevel.Auto,
            "high" => ChatImageDetailLevel.High,
            _ => ChatImageDetailLevel.Low,
        };
}
