using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace GlamourousGallery.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private int designsPerPage;
    private float thumbnailScale;

    public ConfigWindow(Plugin plugin)
        : base("GlamourousGallery Settings###GlamourousGallerySettings")
    {
        Size = new Vector2(360, 150);
        SizeCondition = ImGuiCond.FirstUseEver;
        configuration = plugin.Configuration;
        designsPerPage = Math.Max(1, configuration.DesignsPerPage);
        thumbnailScale = Math.Clamp(configuration.ThumbnailScale <= 0 ? 1f : configuration.ThumbnailScale, 0.25f, 4f);
    }

    public void Dispose() { }

    public override void Draw()
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
        if (ImGui.Checkbox("Show hidden designs", ref showHidden))
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
