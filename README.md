# JLZ Project View Enhancer

Standalone Unity UPM package for Project window enhancements.

## Features

- Folder color and font styling
- Indent guide lines
- Active selection guide highlight
- Alternating row backgrounds
- Two-column folder symbol overlays
- Reflection-based tree view layout patching

## Package Layout

- `Editor/JLZ.ProjectViewEnhancer.Editor.asmdef`: editor assembly
- `Editor/ProjectViewEnhancer`: editor scripts
- `Editor/Icons/ExtractedUnityIcons`: optional icon textures for custom overlays

## Settings

Settings are stored in `ProjectSettings/ProjectViewEnhancerSettings.asset`.

## Install

Add one of the following entries to `Packages/manifest.json`.

Local file dependency:

```json
"com.jlz.project-view-enhancer": "file:../LocalPackages/com.jlz.project-view-enhancer"
```

Git dependency:

```json
"com.jlz.project-view-enhancer": "https://your-git-host/your-org/com.jlz.project-view-enhancer.git#1.0.0"
```

## Notes

- This repository is intended to be used as a local file package or a Git-based UPM package.
- Unity resolves the package in the project as `Packages/com.jlz.project-view-enhancer`.
- Legacy icon paths under `Assets/Game/Art/Editor/ProjectViewEnhancer/ExtractedUnityIcons` are remapped to the package path automatically.
