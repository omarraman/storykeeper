using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class DisabledTextToSpeechProvider : ITextToSpeechProvider
{
    public TextToSpeechProviderKind Kind => TextToSpeechProviderKind.Disabled;

    public Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(TextToSpeechResult.Disabled());
}
