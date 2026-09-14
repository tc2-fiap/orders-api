using System.Text.Json;

namespace FiapGames.Shared.Infrastructure;

// Baked into the image at build time as a file (see Dockerfile) instead of
// passed via --build-arg — a value read from a file already inside the
// image can't be forgotten or computed against a not-yet-committed HEAD the
// way a --build-arg could (both bit this project in practice). See notes.md.
public static class BuildInfo
{
    public static (string Sha, string BuildTime) Read(string path = "build-info.json")
    {
        try
        {
            if (!File.Exists(path))
                return ("unknown", "unknown");

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return (data?.GetValueOrDefault("sha") ?? "unknown", data?.GetValueOrDefault("buildTime") ?? "unknown");
        }
        catch
        {
            return ("unknown", "unknown");
        }
    }
}
