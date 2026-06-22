using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace GlamourousGallery;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public int DesignsPerPage { get; set; } = 10;
    public float ThumbnailScale { get; set; } = 1f;
    public bool ShowAllDesigns { get; set; }
    public bool ShowHiddenDesigns { get; set; }
    public bool OnlyShowQuickDesigns { get; set; }
    public bool ShowFolders { get; set; }
    public bool ShowFoldersBeforeDesigns { get; set; } = true;
    public bool ShowFavoritesOnTop { get; set; }
    public bool ShowFavoriteNameMarker { get; set; } = true;
    public DesignSubtitleMode DesignSubtitle { get; set; } = DesignSubtitleMode.CreationDate;
    public MissingFolderSubtitleMode MissingFolderSubtitle { get; set; } = MissingFolderSubtitleMode.CreationDate;
    public Dictionary<string, GalleryDesignConfig> Designs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GalleryFolderConfig> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, uint> TagColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GalleryCharacterViewConfig> CharacterViewConfigs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Save()
    {
        Version = 2;
        Designs ??= new Dictionary<string, GalleryDesignConfig>(StringComparer.OrdinalIgnoreCase);
        Folders ??= new Dictionary<string, GalleryFolderConfig>(StringComparer.OrdinalIgnoreCase);
        TagColors ??= new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        CharacterViewConfigs ??= new Dictionary<string, GalleryCharacterViewConfig>(StringComparer.OrdinalIgnoreCase);
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public sealed class GalleryCharacterViewConfig
{
    public string Filter { get; set; } = "All";
    public DesignSortMode SortMode { get; set; } = DesignSortMode.Alphabetical;
}

[Serializable]
public sealed class GalleryDesignConfig
{
    public bool Favorite { get; set; }
    public bool Hidden { get; set; }
    public int ThumbnailRevision { get; set; }
    public bool ApplyCustomizations { get; set; } = true;
    public bool ApplyGear { get; set; } = true;
    public List<string> Tags { get; set; } = [];
}

[Serializable]
public sealed class GalleryFolderConfig
{
    public bool Favorite { get; set; }
    public bool Hidden { get; set; }
    public int ThumbnailRevision { get; set; }
}

public enum DesignSubtitleMode
{
    CreationDate,
    LastModifiedDate,
    FolderName,
}

public enum MissingFolderSubtitleMode
{
    CreationDate,
    LastModifiedDate,
    None,
}
