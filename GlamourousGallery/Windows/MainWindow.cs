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
    private string currentFolder = string.Empty;
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

        var allEntries = GetVisibleEntries().ToList();
        var pageSize = Math.Max(1, plugin.Configuration.DesignsPerPage);
        var visibleEntries = plugin.Configuration.ShowAllDesigns
            ? allEntries
            : allEntries.Skip(page * pageSize).Take(pageSize).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(allEntries.Count / (double)pageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        ImGui.Separator();
        DrawGallery(visibleEntries);

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

        if (plugin.Configuration.ShowFolders && !string.IsNullOrEmpty(currentFolder))
        {
            ImGui.SameLine();
            if (ImGui.Button("Back"))
            {
                currentFolder = GetParentFolder(currentFolder);
                page = 0;
            }

            ImGui.SameLine();
            ImGui.TextDisabled(currentFolder);
        }
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

    private IEnumerable<GalleryEntry> GetVisibleEntries()
    {
        if (!plugin.Configuration.ShowFolders)
        {
            currentFolder = string.Empty;
            return SortEntries(GetFilteredDesigns().Select(GalleryEntry.FromDesign));
        }

        var designs = GetFilteredDesigns().ToList();
        if (designs.All(d => !IsInFolder(d.FileSystemFolder, currentFolder)))
            currentFolder = string.Empty;

        var folders = BuildVisibleFolders(currentFolder, designs)
            .Where(f => plugin.Configuration.ShowHiddenDesigns || !plugin.GetFolderConfig(f.Path).Hidden)
            .Where(f => FolderMatchesSearchOrContainsMatches(f.Path, designs));

        var directDesigns = designs.Where(d => string.Equals(d.FileSystemFolder, currentFolder, StringComparison.OrdinalIgnoreCase));
        return SortEntries(folders.Select(GalleryEntry.FromFolder).Concat(directDesigns.Select(GalleryEntry.FromDesign)));
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
                || d.FileSystemFolder.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.GlamourerTags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || plugin.GetDesignConfig(d.Identifier).Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (filter == "Favorites")
            query = query.Where(d => plugin.GetDesignConfig(d.Identifier).Favorite);
        else if (filter != "All")
            query = query.Where(d => d.GlamourerTags.Contains(filter, StringComparer.OrdinalIgnoreCase)
                || plugin.GetDesignConfig(d.Identifier).Tags.Contains(filter, StringComparer.OrdinalIgnoreCase));

        return query;
    }

    private IEnumerable<GalleryEntry> SortEntries(IEnumerable<GalleryEntry> entries)
    {
        var sorted = entries.OrderBy(e => SortBucket(e));
        sorted = sortMode switch
        {
            DesignSortMode.Newest => sorted.ThenByDescending(e => e.CreationDate),
            DesignSortMode.LastUpdated => sorted.ThenByDescending(e => e.LastEdit),
            _ => sorted.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
        };

        return sorted.ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    private int SortBucket(GalleryEntry entry)
    {
        if (plugin.Configuration.ShowFolders && plugin.Configuration.ShowFoldersBeforeDesigns)
        {
            if (entry.IsFolder)
                return plugin.Configuration.ShowFavoritesOnTop && IsFavorite(entry) ? 0 : 1;

            return plugin.Configuration.ShowFavoritesOnTop && IsFavorite(entry) ? 2 : 3;
        }

        return plugin.Configuration.ShowFavoritesOnTop && IsFavorite(entry) ? 0 : 1;
    }

    private IReadOnlyList<FolderEntry> BuildVisibleFolders(string parent, IReadOnlyList<GlamourerDesign> designs)
    {
        return designs
            .Select(d => GetChildFolder(parent, d.FileSystemFolder))
            .Where(f => f != null)
            .Select(f => f!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(f => PromoteEmptyFolder(f, designs))
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private IEnumerable<FolderEntry> PromoteEmptyFolder(string folderPath, IReadOnlyList<GlamourerDesign> designs)
    {
        if (designs.Any(d => string.Equals(d.FileSystemFolder, folderPath, StringComparison.OrdinalIgnoreCase)))
        {
            yield return new FolderEntry(folderPath);
            yield break;
        }

        var children = designs
            .Select(d => GetChildFolder(folderPath, d.FileSystemFolder))
            .Where(f => f != null)
            .Select(f => f!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (children.Count == 0)
        {
            yield return new FolderEntry(folderPath);
            yield break;
        }

        foreach (var child in children.SelectMany(c => PromoteEmptyFolder(c, designs)))
            yield return child;
    }

    private bool FolderMatchesSearchOrContainsMatches(string folderPath, IReadOnlyList<GlamourerDesign> filteredDesigns)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return GetFolderName(folderPath).Contains(search, StringComparison.OrdinalIgnoreCase)
            || filteredDesigns.Any(d => IsInFolder(d.FileSystemFolder, folderPath));
    }

    private static string? GetChildFolder(string parent, string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !IsInFolder(folderPath, parent) || string.Equals(folderPath, parent, StringComparison.OrdinalIgnoreCase))
            return null;

        var remaining = string.IsNullOrEmpty(parent) ? folderPath : folderPath[(parent.Length + 1)..];
        var slash = remaining.IndexOf('/');
        var childName = slash < 0 ? remaining : remaining[..slash];
        return string.IsNullOrEmpty(parent) ? childName : $"{parent}/{childName}";
    }

    private static bool IsInFolder(string folderPath, string parent)
    {
        if (string.IsNullOrEmpty(parent))
            return !string.IsNullOrEmpty(folderPath);

        return folderPath.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || folderPath.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }

    private void DrawGallery(IReadOnlyList<GalleryEntry> entries)
    {
        using var child = ImRaii.Child("GalleryScroll", new Vector2(0, plugin.Configuration.ShowAllDesigns ? 0 : -34), false);
        if (!child.Success)
            return;

        if (entries.Count == 0)
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

        for (var i = 0; i < entries.Count; ++i)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            DrawCard(entries[i], new Vector2(cellWidth, cardHeight), thumbSize);
        }
    }

    private void DrawCard(GalleryEntry entry, Vector2 size, Vector2 thumbSize)
    {
        using var id = ImRaii.PushId(entry.Id);

        if (IsHidden(entry))
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.35f);

        ImGui.BeginGroup();
        DrawThumbnail(entry, thumbSize);
        var thumbnailLeftClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var thumbnailRightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var thumbnailMin = ImGui.GetItemRectMin();
        var thumbnailMax = ImGui.GetItemRectMax();
        var cursorAfterThumbnail = ImGui.GetCursorScreenPos();
        var favoriteBlockedThumbnail = DrawFavoriteButton(entry, thumbnailMin, thumbnailMax);
        ImGui.SetCursorScreenPos(cursorAfterThumbnail);

        if (thumbnailLeftClicked && !favoriteBlockedThumbnail)
        {
            if (entry.Design != null)
                plugin.ApplyDesign(entry.Design, plugin.GetDesignConfig(entry.Design.Identifier));
            else
                OpenFolder(entry.Folder!.Path);
        }

        if (thumbnailRightClicked && !favoriteBlockedThumbnail)
        {
            if (entry.Design != null)
                editor.Open(entry.Design);
            else
                editor.OpenFolder(entry.Folder!.Path);
        }

        var star = IsFavorite(entry) ? "* " : string.Empty;
        var hidden = IsHidden(entry) ? " (hidden)" : string.Empty;
        var displayName = TruncateToWidth($"{star}{entry.Name}{hidden}", size.X);
        ImGui.TextUnformatted(displayName);
        if (ImGui.IsItemHovered() && displayName.EndsWith("...", StringComparison.Ordinal))
            ImGui.SetTooltip($"{star}{entry.Name}{hidden}");

        var subtitle = entry.Design == null ? "Folder" : GetDesignSubtitle(entry.Design);
        if (!string.IsNullOrEmpty(subtitle))
            ImGui.TextDisabled(subtitle);
        else
            ImGui.Dummy(new Vector2(1, ImGui.GetTextLineHeightWithSpacing()));

        ImGui.Dummy(new Vector2(size.X, Math.Max(0, size.Y - thumbSize.Y - ImGui.GetTextLineHeightWithSpacing() * 2)));
        ImGui.EndGroup();

        if (IsHidden(entry))
            ImGui.PopStyleVar();
    }

    private bool DrawFavoriteButton(GalleryEntry entry, Vector2 thumbnailMin, Vector2 thumbnailMax)
    {
        var buttonSize = new Vector2(26, 24) * ImGuiHelpers.GlobalScale;
        var inset = 4 * ImGuiHelpers.GlobalScale;
        var buttonPos = new Vector2(thumbnailMax.X - buttonSize.X - inset, thumbnailMin.Y + inset);
        var buttonMax = buttonPos + buttonSize;
        var hovered = ImGui.IsMouseHoveringRect(buttonPos, buttonMax);
        var clicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var favorite = IsFavorite(entry);

        if (clicked)
        {
            if (entry.Design != null)
                plugin.GetDesignConfig(entry.Design.Identifier).Favorite = !favorite;
            else
                plugin.GetFolderConfig(entry.Folder!.Path).Favorite = !favorite;

            plugin.Configuration.Save();
        }

        var drawList = ImGui.GetWindowDrawList();
        var background = hovered
            ? new Vector4(0.18f, 0.18f, 0.22f, 0.95f)
            : new Vector4(0, 0, 0, 0.45f);
        drawList.AddRectFilled(buttonPos, buttonMax, ImGui.GetColorU32(background), 3 * ImGuiHelpers.GlobalScale);

        var label = favorite ? "★" : "☆";
        var textSize = ImGui.CalcTextSize(label);
        var textPos = buttonPos + (buttonSize - textSize) / 2f;
        drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), label);

        if (hovered)
            ImGui.SetTooltip(favorite ? "Remove favorite" : "Add favorite");

        return hovered;
    }

    private void DrawThumbnail(GalleryEntry entry, Vector2 size)
    {
        var path = entry.Design != null
            ? plugin.ThumbnailPath(entry.Design.Identifier, plugin.GetDesignConfig(entry.Design.Identifier))
            : GetFolderThumbnailPath(entry.Folder!.Path);

        IDalamudTextureWrap? texture = null;
        if (File.Exists(path))
            texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();

        if (texture != null)
            ImGui.Image(texture.Handle, size);
        else
            ImGui.Button($"{entry.Name}###thumbnail", size);

        if (entry.Design != null && TryGetTagColor(entry.Design, out var tagColor))
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRect(min, max, tagColor, 0, ImDrawFlags.None, 3f * ImGuiHelpers.GlobalScale);
        }
    }

    private bool TryGetTagColor(GlamourerDesign design, out uint color)
    {
        foreach (var tag in design.GlamourerTags
                     .Concat(plugin.GetDesignConfig(design.Identifier).Tags)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            if (plugin.Configuration.TagColors.TryGetValue(tag, out var packed))
            {
                color = ImGui.GetColorU32(ColorFromU32(packed));
                return true;
            }
        }

        color = 0;
        return false;
    }

    private bool IsFavorite(GalleryEntry entry)
        => entry.Design != null
            ? plugin.GetDesignConfig(entry.Design.Identifier).Favorite
            : plugin.GetFolderConfig(entry.Folder!.Path).Favorite;

    private bool IsHidden(GalleryEntry entry)
        => entry.Design != null
            ? plugin.GetDesignConfig(entry.Design.Identifier).Hidden
            : plugin.GetFolderConfig(entry.Folder!.Path).Hidden;

    private string GetDesignSubtitle(GlamourerDesign design)
    {
        return plugin.Configuration.DesignSubtitle switch
        {
            DesignSubtitleMode.LastModifiedDate => design.LastEdit.ToLocalTime().ToString("yyyy/MM/dd  HH:mm"),
            DesignSubtitleMode.FolderName when !string.IsNullOrEmpty(design.FileSystemFolder) => GetFolderName(design.FileSystemFolder),
            DesignSubtitleMode.FolderName => plugin.Configuration.MissingFolderSubtitle switch
            {
                MissingFolderSubtitleMode.LastModifiedDate => design.LastEdit.ToLocalTime().ToString("yyyy/MM/dd  HH:mm"),
                MissingFolderSubtitleMode.None => string.Empty,
                _ => design.CreationDate.ToLocalTime().ToString("yyyy/MM/dd  HH:mm"),
            },
            _ => design.CreationDate.ToLocalTime().ToString("yyyy/MM/dd  HH:mm"),
        };
    }

    private string GetFolderThumbnailPath(string folderPath)
    {
        var config = plugin.GetFolderConfig(folderPath);
        var customPath = plugin.FolderThumbnailPath(folderPath, config);
        return File.Exists(customPath) ? customPath : plugin.DefaultFolderIconPath;
    }

    private void OpenFolder(string folderPath)
    {
        currentFolder = folderPath;
        page = 0;
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

    private static string GetParentFolder(string folderPath)
    {
        var index = folderPath.LastIndexOf('/');
        return index < 0 ? string.Empty : folderPath[..index];
    }

    private static string GetFolderName(string folderPath)
    {
        var index = folderPath.LastIndexOf('/');
        return index < 0 ? folderPath : folderPath[(index + 1)..];
    }

    private static Vector4 ColorFromU32(uint color)
        => new(
            (color & 0xFF) / 255f,
            ((color >> 8) & 0xFF) / 255f,
            ((color >> 16) & 0xFF) / 255f,
            ((color >> 24) & 0xFF) / 255f);

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

    private sealed record FolderEntry(string Path)
    {
        public string Name
        {
            get
            {
                var index = Path.LastIndexOf('/');
                return index < 0 ? Path : Path[(index + 1)..];
            }
        }
    }

    private sealed record GalleryEntry(GlamourerDesign? Design, FolderEntry? Folder)
    {
        public bool IsFolder => Folder != null;
        public string Id => Design?.Identifier ?? $"folder:{Folder!.Path}";
        public string Name => Design?.Name ?? Folder!.Name;
        public DateTimeOffset CreationDate => Design?.CreationDate ?? DateTimeOffset.MinValue;
        public DateTimeOffset LastEdit => Design?.LastEdit ?? DateTimeOffset.MinValue;

        public static GalleryEntry FromDesign(GlamourerDesign design) => new(design, null);
        public static GalleryEntry FromFolder(FolderEntry folder) => new(null, folder);
    }
}
