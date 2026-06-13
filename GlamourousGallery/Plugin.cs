using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GlamourousGallery.Windows;
using System;
using System.IO;

namespace GlamourousGallery;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/glamgallery";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public GlamourerDesignProvider DesignProvider { get; } = new();
    public string ThumbnailDirectory { get; }

    public readonly WindowSystem WindowSystem = new("GlamourousGallery");
    private readonly ConfigWindow configWindow;
    private readonly DesignEditorWindow designEditorWindow;
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ThumbnailDirectory = Path.Combine(PluginInterface.ConfigDirectory.FullName, "thumbnails");
        Directory.CreateDirectory(ThumbnailDirectory);

        designEditorWindow = new DesignEditorWindow(this);
        configWindow = new ConfigWindow(this);
        mainWindow = new MainWindow(this, designEditorWindow);

        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(designEditorWindow);
        WindowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close the GlamourousGallery window.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("GlamourousGallery loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        configWindow.Dispose();
        designEditorWindow.Dispose();
        mainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    public GalleryDesignConfig GetDesignConfig(string identifier)
    {
        if (!Configuration.Designs.TryGetValue(identifier, out var config))
        {
            config = new GalleryDesignConfig();
            Configuration.Designs[identifier] = config;
        }

        return config;
    }

    public string ThumbnailPath(string identifier)
        => Path.Combine(ThumbnailDirectory, $"{SanitizeFileName(identifier)}.png");

    public bool HasThumbnail(string identifier)
        => File.Exists(ThumbnailPath(identifier));

    public void ApplyDesign(GlamourerDesign design)
        => ApplyDesign(design, GetDesignConfig(design.Identifier));

    public void ApplyDesign(GlamourerDesign design, GalleryDesignConfig config)
    {
        const ulong once = 0x01;
        const ulong equipment = 0x02;
        const ulong customization = 0x04;

        var flags = once;
        if (config.ApplyGear)
            flags |= equipment;
        if (config.ApplyCustomizations)
            flags |= customization;

        if ((flags & (equipment | customization)) == 0)
        {
            Log.Warning("Skipped applying {Identifier} because both gear and customizations are disabled.", design.Identifier);
            return;
        }

        try
        {
            var designId = Guid.Parse(design.Identifier);
            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null)
            {
                Log.Warning("Skipped applying {Identifier} because no local player object is available.", design.Identifier);
                return;
            }

            var applyDesign = PluginInterface.GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign");
            var result = applyDesign.InvokeFunc(designId, localPlayer.ObjectIndex, 0, flags);
            if (result == 0)
                return;

            Log.Warning(
                "Glamourer IPC returned {Result} while applying {Identifier} with flags {Flags}.",
                result,
                design.Identifier,
                flags);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not apply {Identifier} through Glamourer IPC.", design.Identifier);
            if (config.ApplyGear && config.ApplyCustomizations)
                CommandManager.ProcessCommand($"/glamour apply {design.Identifier} | <me>");
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }

    private void OnCommand(string command, string args)
    {
        if (args.Equals("config", StringComparison.OrdinalIgnoreCase))
            ToggleConfigUi();
        else
            ToggleMainUi();
    }

    public void ToggleConfigUi() => configWindow.Toggle();
    public void ToggleMainUi() => mainWindow.Toggle();
}
