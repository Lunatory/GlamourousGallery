using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace GlamourousGallery.Windows;

public sealed class DesignEditorWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly FileDialogManager fileDialogManager = new();
    private GlamourerDesign? design;
    private string newTag = string.Empty;
    private string imagePath = string.Empty;
    private float zoom = 1f;
    private Vector2 pan;

    public DesignEditorWindow(Plugin plugin)
        : base("Design Options###GlamourousGalleryDesignOptions", ImGuiWindowFlags.AlwaysAutoResize)
    {
        IsOpen = false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 360),
            MaximumSize = new Vector2(680, 760),
        };
        this.plugin = plugin;
    }

    public void Open(GlamourerDesign selectedDesign)
    {
        design = selectedDesign;
        newTag = string.Empty;
        imagePath = string.Empty;
        zoom = 1f;
        pan = Vector2.Zero;
        IsOpen = true;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (design != null)
            WindowName = $"{design.Name}###GlamourousGalleryDesignOptions";
    }

    public override void Draw()
    {
        if (design == null)
        {
            ImGui.TextUnformatted("No design selected.");
            return;
        }

        var config = plugin.GetDesignConfig(design.Identifier);
        ImGui.TextUnformatted(design.Name);
        ImGui.TextDisabled(design.Identifier);
        ImGui.TextUnformatted($"Last updated: {design.LastEdit.ToLocalTime():yyyy/MM/dd  HH:mm}");

        ImGui.Separator();
        var favorite = config.Favorite;
        if (ImGui.Checkbox("Favorite", ref favorite))
        {
            config.Favorite = favorite;
            plugin.Configuration.Save();
        }

        var hidden = config.Hidden;
        var hiddenLabel = hidden ? "Hidden" : "Hide from gallery";
        if (ImGui.Checkbox(hiddenLabel, ref hidden))
        {
            config.Hidden = hidden;
            plugin.Configuration.Save();
        }

        var applyCustomizations = config.ApplyCustomizations;
        if (ImGui.Checkbox("Apply customizations", ref applyCustomizations))
        {
            config.ApplyCustomizations = applyCustomizations;
            plugin.Configuration.Save();
        }

        var applyGear = config.ApplyGear;
        if (ImGui.Checkbox("Apply gear", ref applyGear))
        {
            config.ApplyGear = applyGear;
            plugin.Configuration.Save();
        }

        DrawTags(config);
        DrawImageSelector();
        DrawCropper();
        fileDialogManager.Draw();
    }

    private void DrawTags(GalleryDesignConfig config)
    {
        if (ImGui.CollapsingHeader("Tags", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var tag in design!.GlamourerTags)
                ImGui.TextDisabled($"{tag} (Glamourer)");

            for (var i = config.Tags.Count - 1; i >= 0; --i)
            {
                using var id = ImRaii.PushId(i);
                ImGui.TextUnformatted(config.Tags[i]);
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    config.Tags.RemoveAt(i);
                    plugin.Configuration.Save();
                }
            }

            ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("###NewTag", "New tag", ref newTag, 64);
            ImGui.SameLine();
            if (ImGui.Button("Add") && !string.IsNullOrWhiteSpace(newTag))
            {
                var tag = newTag.Trim();
                if (!config.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    config.Tags.Add(tag);
                    config.Tags.Sort(StringComparer.OrdinalIgnoreCase);
                    plugin.Configuration.Save();
                }

                newTag = string.Empty;
            }
        }
    }

    private void DrawImageSelector()
    {
        if (!ImGui.CollapsingHeader("Thumbnail", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var currentThumbnail = plugin.ThumbnailPath(design!.Identifier);
        ImGui.TextUnformatted(File.Exists(currentThumbnail) ? "Custom thumbnail set." : "No custom thumbnail set.");

        if (ImGui.Button("Select PNG"))
            fileDialogManager.OpenFileDialog("Select Thumbnail Image", ".png", OnImageSelected);

        if (File.Exists(currentThumbnail))
        {
            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                File.Delete(currentThumbnail);
            }
        }
    }

    private void OnImageSelected(bool success, string path)
    {
        if (!success || !File.Exists(path))
            return;

        imagePath = path;
        zoom = 1f;
        pan = Vector2.Zero;
    }

    private void DrawCropper()
    {
        if (!File.Exists(imagePath))
            return;

        ImGui.Separator();
        ImGui.TextUnformatted("Crop Preview");
        ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Zoom", ref zoom, 1f, 4f);
        ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Pan X", ref pan.X, -1f, 1f);
        ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Pan Y", ref pan.Y, -1f, 1f);

        var frameSize = new Vector2(220, 220 / ImageCropper.PortraitAspect) * ImGuiHelpers.GlobalScale;
        DrawPreview(frameSize);

        if (ImGui.Button("Save Thumbnail"))
        {
            try
            {
                ImageCropper.SaveCroppedPng(imagePath, plugin.ThumbnailPath(design!.Identifier), zoom, pan);
                imagePath = string.Empty;
                zoom = 1f;
                pan = Vector2.Zero;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Could not save cropped thumbnail for {Identifier}.", design!.Identifier);
            }
        }
    }

    private void DrawPreview(Vector2 frameSize)
    {
        IDalamudTextureWrap? texture = Plugin.TextureProvider.GetFromFile(imagePath).GetWrapOrDefault();
        if (texture == null)
        {
            ImGui.TextUnformatted("Could not load image preview.");
            return;
        }

        var canvasSize = frameSize + new Vector2(140, 100) * ImGuiHelpers.GlobalScale;
        var pos = ImGui.GetCursorScreenPos();
        var framePos = pos + (canvasSize - frameSize) / 2f;
        ImGui.InvisibleButton("###CropPreview", canvasSize);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + canvasSize, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));

        var sourceAspect = texture.Size.X / texture.Size.Y;
        var frameAspect = frameSize.X / frameSize.Y;
        var baseScale = sourceAspect > frameAspect
            ? frameSize.Y / texture.Size.Y
            : frameSize.X / texture.Size.X;
        var scaledSize = texture.Size * baseScale * zoom;
        var maxOffset = Vector2.Max((scaledSize - frameSize) / 2f, Vector2.Zero);
        var imagePan = new Vector2(-pan.X, pan.Y);
        var imagePos = framePos + (frameSize - scaledSize) / 2f + imagePan * maxOffset;
        drawList.PushClipRect(pos, pos + canvasSize, true);
        drawList.AddImage(texture.Handle, imagePos, imagePos + scaledSize);
        drawList.PopClipRect();

        var frameMax = framePos + frameSize;
        var dimColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 0.55f));
        drawList.AddRectFilled(pos, new Vector2(pos.X + canvasSize.X, framePos.Y), dimColor);
        drawList.AddRectFilled(new Vector2(pos.X, frameMax.Y), pos + canvasSize, dimColor);
        drawList.AddRectFilled(new Vector2(pos.X, framePos.Y), new Vector2(framePos.X, frameMax.Y), dimColor);
        drawList.AddRectFilled(new Vector2(frameMax.X, framePos.Y), new Vector2(pos.X + canvasSize.X, frameMax.Y), dimColor);
        drawList.AddRect(framePos, frameMax, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 0, ImDrawFlags.None, 2f);
    }
}
