# Plugin Preset Manager

A plugin preset manager for Dalamud with always-on plugin support.

## Features

- **Create and Manage Presets**: Save and apply different plugin configurations
- **Always-On Plugins**: Mark essential plugins that should always be enabled regardless of preset
- **Default Preset**: Auto-apply a preset when you log in
- **Preview Changes**: See exactly what will change before applying a preset

## Commands

- `/ppreset` - Open the Plugin Preset Manager window
- `/ppm` - Toggle the main window
- `/ppm <preset name>` - Apply a preset by name
- `/ppm alwayson` - Enable only always-on plugins, disable everything else

## Usage

### Creating a Preset
1. Open the Preset Manager (`/ppreset`)
2. Go to the "Presets" tab
3. Enter a name and click "Create Empty"
4. Click "Add Enabled Plugins" to add currently enabled plugins, or manually add plugins

### Always-On Plugins
1. Go to the "Always-On Plugins" tab
2. Click "Add Plugin to Always-On"
3. Select the plugins you want always enabled

Alternatively, use the "All Plugins" tab and check the "Always-On" checkbox for any plugin.

### Applying a Preset
1. Select a preset from the list
2. Review the preview showing what will change
3. Click "Apply"

The preset will enable all plugins in the preset PLUS any always-on plugins, and disable everything else.

### Default Preset
1. Select a preset
2. Click "Set Default"
3. This preset will now automatically apply when you log in

