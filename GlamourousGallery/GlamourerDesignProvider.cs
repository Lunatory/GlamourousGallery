using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GlamourousGallery;

public sealed class GlamourerDesignProvider
{
    private readonly string designDirectory;
    private DateTime lastRefresh = DateTime.MinValue;
    private List<GlamourerDesign> designs = [];

    public GlamourerDesignProvider()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        designDirectory = Path.Combine(appData, "XIVLauncher", "pluginConfigs", "Glamourer", "designs");
    }

    public string DesignDirectory => designDirectory;

    public IReadOnlyList<GlamourerDesign> GetDesigns(bool forceRefresh = false)
    {
        if (forceRefresh || (DateTime.UtcNow - lastRefresh).TotalSeconds > 5)
            Refresh();

        return designs;
    }

    public void Refresh()
    {
        lastRefresh = DateTime.UtcNow;
        if (!Directory.Exists(designDirectory))
        {
            designs = [];
            return;
        }

        var loaded = new List<GlamourerDesign>();
        foreach (var file in Directory.EnumerateFiles(designDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var root = document.RootElement;
                var identifier = ReadString(root, "Identifier");
                var name = ReadString(root, "Name");
                if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(name))
                    continue;

                var creationDate = ReadDate(root, "CreationDate") ?? File.GetCreationTimeUtc(file);
                var lastEdit = ReadDate(root, "LastEdit") ?? creationDate;
                loaded.Add(new GlamourerDesign
                {
                    Identifier = identifier,
                    Name = name,
                    CreationDate = creationDate,
                    LastEdit = lastEdit < creationDate ? creationDate : lastEdit,
                    QuickDesign = ReadBool(root, "QuickDesign") ?? true,
                    SourceFile = file,
                    FileSystemFolder = NormalizeFolderPath(ReadString(root, "FileSystemFolder")),
                    GlamourerTags = ReadTags(root),
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not read Glamourer design file {File}.", file);
            }
        }

        designs = loaded
            .GroupBy(d => d.Identifier, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(d => d.LastEdit).First())
            .ToList();
    }

    private static string ReadString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string NormalizeFolderPath(string path)
        => string.Join(
            '/',
            path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static DateTimeOffset? ReadDate(JsonElement root, string property)
        => root.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static bool? ReadBool(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static IReadOnlyList<string> ReadTags(JsonElement root)
    {
        if (!root.TryGetProperty("Tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return [];

        return tags.EnumerateArray()
            .Where(t => t.ValueKind == JsonValueKind.String)
            .Select(t => t.GetString())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
