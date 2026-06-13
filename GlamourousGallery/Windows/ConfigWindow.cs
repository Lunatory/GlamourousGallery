using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace GlamourousGallery.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private int designsPerPage;
    private float thumbnailScale;

    public ConfigWindow(Plugin plugin)
        : base("GlamourousGallery Settings###GlamourousGallerySettings")
    {
        Size = new Vector2(440, 330);
        SizeCondition = ImGuiCond.FirstUseEver;
        this.plugin = plugin;
        configuration = plugin.Configuration;
        designsPerPage = Math.Max(1, configuration.DesignsPerPage);
        thumbnailScale = Math.Clamp(configuration.ThumbnailScale <= 0 ? 1f : configuration.ThumbnailScale, 0.25f, 4f);
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("###SettingsTabs"))
            return;

        if (ImGui.BeginTabItem("Gallery Options"))
        {
            DrawGalleryOptions();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Tag Options"))
        {
            DrawTagOptions();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawGalleryOptions()
    {
        var showAll = configuration.ShowAllDesigns;
        if (ImGui.Checkbox("Show all designs in one scroll", ref showAll))
        {
            configuration.ShowAllDesigns = showAll;
            configuration.Save();
        }

        using (var disabled = new DisabledScope(configuration.ShowAllDesigns))
        {
            if (ImGui.InputInt("Designs per page", ref designsPerPage))
            {
                designsPerPage = Math.Clamp(designsPerPage, 1, 500);
                configuration.DesignsPerPage = designsPerPage;
                configuration.Save();
            }
        }

        ImGui.SetNextItemWidth(140);
        if (ImGui.InputFloat("Thumbnail scale", ref thumbnailScale, 0.1f, 0.25f, "%.2f"))
        {
            thumbnailScale = Math.Clamp(thumbnailScale, 0.25f, 4f);
            configuration.ThumbnailScale = thumbnailScale;
            configuration.Save();
        }

        var showHidden = configuration.ShowHiddenDesigns;
        if (ImGui.Checkbox("Show hidden designs and folders", ref showHidden))
        {
            configuration.ShowHiddenDesigns = showHidden;
            configuration.Save();
        }

        var onlyQuickDesigns = configuration.OnlyShowQuickDesigns;
        if (ImGui.Checkbox("Only show designs displayed in the quick design bar", ref onlyQuickDesigns))
        {
            configuration.OnlyShowQuickDesigns = onlyQuickDesigns;
            configuration.Save();
        }

        ImGui.Separator();
        DrawSubtitleOptions();
        ImGui.Separator();
        DrawFolderOptions();
    }

    private void DrawSubtitleOptions()
    {
        var subtitle = configuration.DesignSubtitle;
        if (ImGui.BeginCombo("Design subtitle", GetSubtitleLabel(subtitle)))
        {
            DrawSubtitleOption("Creation date and time", DesignSubtitleMode.CreationDate);
            DrawSubtitleOption("Last modified date and time", DesignSubtitleMode.LastModifiedDate);
            DrawSubtitleOption("Folder name", DesignSubtitleMode.FolderName);
            ImGui.EndCombo();
        }

        using (var disabled = new DisabledScope(configuration.DesignSubtitle != DesignSubtitleMode.FolderName))
        {
            if (ImGui.BeginCombo("If design has no folder", GetMissingFolderSubtitleLabel(configuration.MissingFolderSubtitle)))
            {
                DrawMissingFolderSubtitleOption("Creation date and time", MissingFolderSubtitleMode.CreationDate);
                DrawMissingFolderSubtitleOption("Last modified date and time", MissingFolderSubtitleMode.LastModifiedDate);
                DrawMissingFolderSubtitleOption("None", MissingFolderSubtitleMode.None);
                ImGui.EndCombo();
            }
        }
    }

    private void DrawFolderOptions()
    {
        var showFolders = configuration.ShowFolders;
        if (ImGui.Checkbox("Show folders", ref showFolders))
        {
            configuration.ShowFolders = showFolders;
            configuration.Save();
        }

        using (var disabled = new DisabledScope(!configuration.ShowFolders))
        {
            var foldersBeforeDesigns = configuration.ShowFoldersBeforeDesigns;
            if (ImGui.Checkbox("Show folders before designs", ref foldersBeforeDesigns))
            {
                configuration.ShowFoldersBeforeDesigns = foldersBeforeDesigns;
                configuration.Save();
            }
        }

        var favoritesOnTop = configuration.ShowFavoritesOnTop;
        if (ImGui.Checkbox("Show favorites on top", ref favoritesOnTop))
        {
            configuration.ShowFavoritesOnTop = favoritesOnTop;
            configuration.Save();
        }

        var showFavoriteNameMarker = configuration.ShowFavoriteNameMarker;
        if (ImGui.Checkbox("Show * before favorite names", ref showFavoriteNameMarker))
        {
            configuration.ShowFavoriteNameMarker = showFavoriteNameMarker;
            configuration.Save();
        }
    }

    private void DrawTagOptions()
    {
        var tags = configuration.Designs.Values
            .SelectMany(c => c.Tags)
            .Concat(plugin.DesignProvider.GetDesigns().SelectMany(d => d.GlamourerTags))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count == 0)
        {
            ImGui.TextUnformatted("No tags found.");
            return;
        }

        foreach (var tag in tags)
        {
            using var id = ImRaii.PushId(tag);
            var color = configuration.TagColors.TryGetValue(tag, out var packed)
                ? ColorFromU32(packed)
                : new Vector4(0, 0, 0, 0);

            var previewColor = color.W > 0 ? color : new Vector4(0.25f, 0.25f, 0.25f, 1f);
            ImGui.ColorButton("###TagColor", previewColor, ImGuiColorEditFlags.NoTooltip, new Vector2(22, 22));

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                configuration.TagColors.Remove(tag);
                configuration.Save();
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                ImGui.OpenPopup("ColorPicker");

            if (ImGui.BeginPopup("ColorPicker"))
            {
                if (ImGui.ColorPicker4("###Picker", ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                {
                    color.W = 1f;
                    configuration.TagColors[tag] = ColorToU32(color);
                    configuration.Save();
                }

                var hex = ColorToHex(color);
                ImGui.SetNextItemWidth(110);
                if (ImGui.InputText("Hex", ref hex, 7, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase))
                {
                    if (TryParseHexColor(hex, out var parsed))
                    {
                        configuration.TagColors[tag] = ColorToU32(parsed);
                        configuration.Save();
                    }
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(tag);
        }
    }

    private void DrawSubtitleOption(string label, DesignSubtitleMode mode)
    {
        if (ImGui.Selectable(label, configuration.DesignSubtitle == mode))
        {
            configuration.DesignSubtitle = mode;
            configuration.Save();
        }
    }

    private void DrawMissingFolderSubtitleOption(string label, MissingFolderSubtitleMode mode)
    {
        if (ImGui.Selectable(label, configuration.MissingFolderSubtitle == mode))
        {
            configuration.MissingFolderSubtitle = mode;
            configuration.Save();
        }
    }

    private static string GetSubtitleLabel(DesignSubtitleMode mode)
        => mode switch
        {
            DesignSubtitleMode.LastModifiedDate => "Last modified date and time",
            DesignSubtitleMode.FolderName => "Folder name",
            _ => "Creation date and time",
        };

    private static string GetMissingFolderSubtitleLabel(MissingFolderSubtitleMode mode)
        => mode switch
        {
            MissingFolderSubtitleMode.LastModifiedDate => "Last modified date and time",
            MissingFolderSubtitleMode.None => "None",
            _ => "Creation date and time",
        };

    private static Vector4 ColorFromU32(uint color)
        => new(
            (color & 0xFF) / 255f,
            ((color >> 8) & 0xFF) / 255f,
            ((color >> 16) & 0xFF) / 255f,
            ((color >> 24) & 0xFF) / 255f);

    private static uint ColorToU32(Vector4 color)
        => (uint)(Math.Clamp(color.X, 0, 1) * 255)
            | ((uint)(Math.Clamp(color.Y, 0, 1) * 255) << 8)
            | ((uint)(Math.Clamp(color.Z, 0, 1) * 255) << 16)
            | ((uint)(Math.Clamp(color.W, 0, 1) * 255) << 24);

    private static string ColorToHex(Vector4 color)
        => $"{(int)(Math.Clamp(color.X, 0, 1) * 255):X2}{(int)(Math.Clamp(color.Y, 0, 1) * 255):X2}{(int)(Math.Clamp(color.Z, 0, 1) * 255):X2}";

    private static bool TryParseHexColor(string value, out Vector4 color)
    {
        value = value.Trim().TrimStart('#');
        if (value.Length == 6 && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var packed))
        {
            color = new Vector4(
                ((packed >> 16) & 0xFF) / 255f,
                ((packed >> 8) & 0xFF) / 255f,
                (packed & 0xFF) / 255f,
                1f);
            return true;
        }

        color = default;
        return false;
    }

    private readonly ref struct DisabledScope
    {
        private readonly bool disabled;

        public DisabledScope(bool disabled)
        {
            this.disabled = disabled;
            if (disabled)
                ImGui.BeginDisabled();
        }

        public void Dispose()
        {
            if (disabled)
                ImGui.EndDisabled();
        }
    }
}
