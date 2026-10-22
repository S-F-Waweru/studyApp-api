using System.Text.Json;
using StudyApp.Application.Embeddings;

namespace StudyApp.Infrastructure.Embeddings.Readers;

public record ScribbleImage(byte[] Bytes, string ContentType, string? FrameName);

public class ScribbleCanvasExtractor
{
    public (ExtractionResult Result, List<ScribbleImage> Images) Extract(string title, string canvasJson)
    {
        var blocks = new List<ExtractedBlock>();
        var images = new List<ScribbleImage>();
        var order = 0;

        blocks.Add(new ExtractedBlock(BlockKind.Heading, title, null, null, order++));

        JsonDocument doc;
        try { doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson); }
        catch (JsonException) { return (ExtractionResult.Failed("Scribble canvas data is not valid JSON."), images); }

        using (doc)
        {
            // Frames first: a text element must be able to resolve the frame it sits on
            // even when that frame is declared later in the scene array.
            var frames = ReadFrames(doc.RootElement);

            if (doc.RootElement.TryGetProperty("elements", out var elements))
            {
                foreach (var element in elements.EnumerateArray())
                {
                    // deleted elements must never be indexedpa
                    if (element.TryGetProperty("isDeleted", out var deleted) && deleted.ValueKind == JsonValueKind.True)
                        continue;

                    var type = element.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var heading = FrameOf(element, frames);

                    if (type == "text" && element.TryGetProperty("text", out var text))
                    {
                        var value = text.GetString();
                        if (string.IsNullOrWhiteSpace(value)) continue;

                        blocks.Add(new ExtractedBlock(BlockKind.Text, value.Trim(), heading, heading, order++));
                    }
                    else if (type == "image" && element.TryGetProperty("fileId", out var fileId))
                    {
                        var image = ReadImage(doc.RootElement, fileId.GetString(), heading);
                        if (image is null) continue;

                        images.Add(image);
                        blocks.Add(new ExtractedBlock(BlockKind.Image, null, heading, heading, order++,
                                                     image.Bytes, image.ContentType));
                    }
                }
            }
        }

        return (new ExtractionResult(blocks, SuggestedTitle: title), images);
    }

    private static Dictionary<string, string> ReadFrames(JsonElement root)
    {
        var frames = new Dictionary<string, string>();
        if (!root.TryGetProperty("elements", out var elements)) return frames;

        foreach (var element in elements.EnumerateArray())
        {
            if (element.TryGetProperty("type", out var type) && type.GetString() != "frame") continue;
            if (!element.TryGetProperty("id", out var id) || id.GetString() is not { } idValue) continue;
            if (!element.TryGetProperty("name", out var name)) continue;
            if (string.IsNullOrWhiteSpace(name.GetString()) || name.GetString() is not { } nameValue) continue;

            frames[idValue] = nameValue;
        }

        return frames;
    }

    // Excalidraw groups shapes into frames by containerId/frameId; use the frame name as the breadcrumb.
    private static string? FrameOf(JsonElement element, IReadOnlyDictionary<string, string> frames)
    {
        if (element.TryGetProperty("frameId", out var frameId)
            && frameId.GetString() is { } id
            && frames.TryGetValue(id, out var name))
            return name;

        return null;
    }

    // Pasted/dropped images live in the scene's "files" map as base64 data URLs.
    private static ScribbleImage? ReadImage(JsonElement root, string? fileId, string? frameName)
    {
        if (string.IsNullOrEmpty(fileId)) return null;
        if (!root.TryGetProperty("files", out var files)) return null;
        if (!files.TryGetProperty(fileId, out var file)) return null;

        var dataUrl = file.TryGetProperty("dataURL", out var d) ? d.GetString() : null;
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;

        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return null;

        var header = dataUrl[..comma];                 // "data:image/png;base64"
        var payload = dataUrl[(comma + 1)..];
        var contentType = header.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? header[5..].Split(';')[0]
            : "image/png";

        try { return new ScribbleImage(Convert.FromBase64String(payload), contentType, frameName); }
        catch (FormatException) { return null; }
    }
}