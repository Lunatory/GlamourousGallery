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
    public Dictionary<string, GalleryDesignConfig> Designs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
    public bool ApplyCustomizations { get; set; } = true;
    public bool ApplyGear { get; set; } = true;
    public List<string> Tags { get; set; } = [];
}
