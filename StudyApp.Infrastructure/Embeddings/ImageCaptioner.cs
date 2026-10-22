using StudyApp.Application.Ai;
using StudyApp.Application.Repositories;
using StudyApp.Application.Storage;

namespace StudyApp.Infrastructure.Embeddings;

public interface IImageCaptioner
{
    Task<string?> CaptionAsync(byte[] image, string contentType, CancellationToken ct = default);
}

public class ImageCaptioner : IImageCaptioner
{
    private const string Prompt =
        "You are preparing study material. Transcribe ALL text visible in this image, then describe " +
        "the diagram or figure: its components, labels, and the relationships between them. " +
        "Be factual and dense; do not speculate.";

    private readonly IAiSettingsRepository _settings;
    private readonly IEnumerable<IVisionProvider> _providers;
    private readonly IFileStorage _storage;

    public ImageCaptioner(IAiSettingsRepository settings, IEnumerable<IVisionProvider> providers, IFileStorage storage)
    {
        _settings = settings; _providers = providers; _storage = storage;
    }

    public async Task<string?> CaptionAsync(byte[] image, string contentType, CancellationToken ct = default)
    {
        var config = (await _settings.GetAsync()).ToVisionConfig();
        var provider = _providers.FirstOrDefault(p => p.ProviderType == config.Provider);
        if (provider is null) return null; // no vision model -> caller marks the page Skipped

        try
        {
            var caption = await provider.DescribeAsync(config, image, contentType, Prompt, ct);
            return string.IsNullOrWhiteSpace(caption) ? null : caption;
        }
        catch (Exception)
        {
            return null; // captioning is best-effort; never fail the whole document for one image
        }
    }
}