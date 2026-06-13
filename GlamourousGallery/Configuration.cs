using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace GlamourousGallery;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public int DesignsPerPage { get; set; } = 10;
    public float ThumbnailScale { get; set; } = 1f;
    public bool ShowAllDesigns { get; set; }
    public bool ShowHiddenDesigns { get; set; }
    public bool OnlyShowQuickDesigns { get; set; }
    public bool ShowFolders { get; set; }
    public bool ShowFoldersBeforeDesigns { get; set; } = true;
    public bool ShowFavoritesOnTop { get; set; }
    public DesignSubtitleMode DesignSubtitle { get; set; } = DesignSubtitleMode.CreationDate;
    public MissingFolderSubtitleMode MissingFolderSubtitle { get; set; } = MissingFolderSubtitleMode.CreationDate;
    public Dictionary<string, GalleryDesignConfig> Designs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GalleryFolderConfig> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, uint> TagColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
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
