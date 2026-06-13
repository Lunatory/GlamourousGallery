using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace GlamourousGallery.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly DesignEditorWindow editor;
    private string search = string.Empty;
    private DesignSortMode sortMode = DesignSortMode.Alphabetical;
    private string filter = "All";
    private int page;

    public MainWindow(Plugin plugin, DesignEditorWindow editor)
        : base("GlamourousGallery###GlamourousGalleryMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 460),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.plugin = plugin;
        this.editor = editor;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawToolbar();

        var allDesigns = GetFilteredDesigns().ToList();
        var pageSize = Math.Max(1, plugin.Configuration.DesignsPerPage);
        var visibleDesigns = plugin.Configuration.ShowAllDesigns
            ? allDesigns
            : allDesigns.Skip(page * pageSize).Take(pageSize).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(allDesigns.Count / (double)pageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        ImGui.Separator();
        DrawGallery(visibleDesigns);

        if (!plugin.Configuration.ShowAllDesigns)
            DrawPager(totalPages);
    }

    private void DrawToolbar()
    {
        var available = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(Math.Max(160, available - 300));
        if (ImGui.InputTextWithHint("###Search", "Search Bar", ref search, 128))
            page = 0;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(135 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###Filter", filter))
        {
            foreach (var option in BuildFilterOptions())
            {
                if (ImGui.Selectable(option, option == filter))
                {
                    filter = option;
                    page = 0;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        var sortLabel = sortMode switch
        {
            DesignSortMode.Newest => "Newest",
            DesignSortMode.LastUpdated => "Last updated",
            _ => "Alphabetical",
        };

        if (ImGui.BeginCombo("###Sort", sortLabel))
        {
            DrawSortOption("Alphabetical", DesignSortMode.Alphabetical);
            DrawSortOption("Newest", DesignSortMode.Newest);
            DrawSortOption("Last updated", DesignSortMode.LastUpdated);
            ImGui.EndCombo();
        }

        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();
        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
            plugin.DesignProvider.Refresh();
    }

    private void DrawSortOption(string label, DesignSortMode mode)
    {
        if (ImGui.Selectable(label, sortMode == mode))
        {
            sortMode = mode;
            page = 0;
        }
    }

    private IEnumerable<string> BuildFilterOptions()
    {
        yield return "All";
        yield return "Favorites";

        foreach (var tag in plugin.Configuration.Designs.Values
                     .SelectMany(c => c.Tags)
                     .Concat(plugin.DesignProvider.GetDesigns().SelectMany(d => d.GlamourerTags))
                     .Where(t => !string.IsNullOrWhiteSpace(t))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            yield return tag;
        }
    }

    private IEnumerable<GlamourerDesign> GetFilteredDesigns()
    {
        var query = plugin.DesignProvider.GetDesigns().AsEnumerable();
        if (!plugin.Configuration.ShowHiddenDesigns)
            query = query.Where(d => !plugin.GetDesignConfig(d.Identifier).Hidden);
        if (plugin.Configuration.OnlyShowQuickDesigns)
            query = query.Where(d => d.QuickDesign);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Identifier.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.GlamourerTags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || plugin.GetDesignConfig(d.Identifier).Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (filter == "Favorites")
            query = query.Where(d => plugin.GetDesignConfig(d.Identifier).Favorite);
        else if (filter != "All")
            query = query.Where(d => d.GlamourerTags.Contains(filter, StringComparer.OrdinalIgnoreCase)
                || plugin.GetDesignConfig(d.Identifier).Tags.Contains(filter, StringComparer.OrdinalIgnoreCase));

        IOrderedEnumerable<GlamourerDesign> ordered = sortMode switch
        {
            DesignSortMode.Newest => query.OrderByDescending(d => d.CreationDate),
            DesignSortMode.LastUpdated => query.OrderByDescending(d => d.LastEdit),
            _ => query.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase),
        };

        return ordered.ThenBy(d => d.Identifier, StringComparer.OrdinalIgnoreCase);
    }

    private void DrawGallery(IReadOnlyList<GlamourerDesign> designs)
    {
        using var child = ImRaii.Child("GalleryScroll", new Vector2(0, plugin.Configuration.ShowAllDesigns ? 0 : -34), false);
        if (!child.Success)
            return;

        if (designs.Count == 0)
        {
            ImGui.TextUnformatted(Directory.Exists(plugin.DesignProvider.DesignDirectory)
                ? "No designs match the current filters."
                : $"Glamourer design folder was not found: {plugin.DesignProvider.DesignDirectory}");
            return;
        }

        var style = ImGui.GetStyle();
        var thumbnailScale = Math.Clamp(plugin.Configuration.ThumbnailScale <= 0 ? 1f : plugin.Configuration.ThumbnailScale, 0.25f, 4f);
        var cellWidth = 150 * thumbnailScale * ImGuiHelpers.GlobalScale;
        var cardHeight = 272 * thumbnailScale * ImGuiHelpers.GlobalScale;
        var columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (cellWidth + style.ItemSpacing.X)));
        var thumbSize = new Vector2(cellWidth, cellWidth / ImageCropper.PortraitAspect);

        for (var i = 0; i < designs.Count; ++i)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            DrawCard(designs[i], new Vector2(cellWidth, cardHeight), thumbSize);
        }
    }

    private void DrawCard(GlamourerDesign design, Vector2 size, Vector2 thumbSize)
    {
        var config = plugin.GetDesignConfig(design.Identifier);
        using var id = ImRaii.PushId(design.Identifier);

        if (config.Hidden)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.35f);

        ImGui.BeginGroup();
        DrawThumbnail(design, thumbSize);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            plugin.ApplyDesign(design, config);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            editor.Open(design);

        var star = config.Favorite ? "* " : string.Empty;
        var hidden = config.Hidden ? " (hidden)" : string.Empty;
        var displayName = TruncateToWidth($"{star}{design.Name}{hidden}", size.X);
        ImGui.TextUnformatted(displayName);
        if (ImGui.IsItemHovered() && displayName.EndsWith("...", StringComparison.Ordinal))
            ImGui.SetTooltip($"{star}{design.Name}{hidden}");
        ImGui.TextDisabled(design.LastEdit.ToLocalTime().ToString("yyyy/MM/dd  HH:mm"));
        ImGui.Dummy(new Vector2(size.X, Math.Max(0, size.Y - thumbSize.Y - ImGui.GetTextLineHeightWithSpacing() * 2)));
        ImGui.EndGroup();

        if (config.Hidden)
            ImGui.PopStyleVar();
    }

    private void DrawThumbnail(GlamourerDesign design, Vector2 size)
    {
        var path = plugin.ThumbnailPath(design.Identifier);
        IDalamudTextureWrap? texture = null;
        if (File.Exists(path))
            texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();

        if (texture != null)
        {
            ImGui.Image(texture.Handle, size);
        }
        else
        {
            ImGui.Button($"{design.Name}###thumbnail", size);
        }
    }

    private void DrawPager(int totalPages)
    {
        ImGui.Separator();
        var width = ImGui.GetContentRegionAvail().X;
        var text = $"{page + 1} / {totalPages}";
        ImGui.SetCursorPosX(Math.Max(0, (width - 250 * ImGuiHelpers.GlobalScale) / 2));

        if (ImGui.Button("|<"))
            page = 0;
        ImGui.SameLine();
        if (ImGui.Button("<"))
            page = Math.Max(0, page - 1);
        ImGui.SameLine();
        ImGui.TextUnformatted(text);
        ImGui.SameLine();
        if (ImGui.Button(">"))
            page = Math.Min(totalPages - 1, page + 1);
        ImGui.SameLine();
        if (ImGui.Button(">|"))
            page = totalPages - 1;
    }

    private static string TruncateToWidth(string text, float maxWidth)
    {
        if (ImGui.CalcTextSize(text).X <= maxWidth)
            return text;

        const string ellipsis = "...";
        var ellipsisWidth = ImGui.CalcTextSize(ellipsis).X;
        var availableWidth = Math.Max(0, maxWidth - ellipsisWidth);
        var length = text.Length;
        while (length > 0 && ImGui.CalcTextSize(text[..length]).X > availableWidth)
            --length;

        return length <= 0 ? ellipsis : text[..length].TrimEnd() + ellipsis;
    }
}
