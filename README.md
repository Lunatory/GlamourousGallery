# GlamourousGallery

GlamourousGallery is a Dalamud plugin that presents Glamourer designs as a visual gallery.

## Features

* Reads Glamourer design metadata directly from `%APPDATA%\XIVLauncher\pluginConfigs\Glamourer\designs`.
* Shows one gallery entry per Glamourer design identifier.
* Displays custom portrait thumbnails, or the design name when no thumbnail has been set.
* Applies a design with left click through Glamourer's IPC, including per-design gear/customization toggles.
* Opens per-design options with right click for favorites, hidden state, application toggles, custom tags, and thumbnail cropping.
* Supports search, favorites/tag filters, alphabetical/newest/last-updated sorting, paging, and an all-designs scroll mode.

## Building

1. Open `GlamourousGallery.sln`.
2. Build the solution in Debug or Release.
3. The plugin DLL is written to `GlamourousGallery/bin/x64/Debug/GlamourousGallery.dll` or the matching Release folder.

## In Game

Use `/glamgallery` to open or close the main gallery window.

Custom cropped thumbnails are saved to `%APPDATA%\XIVLauncher\pluginConfigs\GlamourousGallery\thumbnails` as `<Glamourer Identifier>.png`.

## Notes

This plugin intentionally avoids a compile-time dependency on Glamourer or Penumbra. Glamourer remains the source of truth for designs, and GlamourousGallery stores only gallery-specific metadata keyed by Glamourer design identifier.

## Disclaimer

This plugin is almost entirely AI-generated, with minimal human changes. If you are not comfortable with this please do not install it.