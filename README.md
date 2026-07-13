# 3D-to-Pixel-Art Smear Generator

A Unity editor package for baking 3D character animation into pixel-art sprite animation, with optional smear frames generated from character motion.

The generator runs in the Unity Editor. It samples an animation clip, measures bone and vertex motion, creates smear geometry, captures high-resolution frames, converts those frames to pixel art, and exports a sprite animation package.

## What the generator produces

A full bake creates:

- a pixel-art sprite sheet PNG;
- `animation.json` with frame rectangles, timing, pivot, and smear metadata;
- `package.json` identifying the portable animation folder;
- a Unity `AnimationClip` that changes `SpriteRenderer.m_Sprite`;
- an `AnimatorController` containing the generated clip; and
- a prefab with `SpriteRenderer` and `Animator` components.

Unity project output is written under:

```text
Assets/SmearGenerator.Generated/
├── Output/
├── ImportedPackages/
├── Diagnostics/
└── Temp/
```

## Requirements

- Unity `6000.3` or newer in the Unity 6.3 line
- Verified development version: Unity `6000.3.6f1`
- A 3D character with a `SkinnedMeshRenderer`
- An animation clip compatible with the character

The core package has no external package dependencies. The included tests use Unity Test Framework `1.6.0`.

See [Dependencies](Documentation~/dependencies.md) for details.

## Install

### From GitHub

In Unity, open **Window > Package Management > Package Manager**, select the plus button, and choose **Add package from git URL**. Enter:

```text
https://github.com/Loxenary/3D-to-Pixel-Art-Smear-Generator.git#v0.2.2
```

The same dependency can be added to the target project's `Packages/manifest.json`:

```json
"com.davis.smear-generator": "https://github.com/Loxenary/3D-to-Pixel-Art-Smear-Generator.git#v0.2.2"
```

### From a local clone

Select **Add package from disk** and choose this repository's `package.json`, or add a local dependency:

```json
"com.davis.smear-generator": "file:/absolute/path/to/3D-to-Pixel-Art-Smear-Generator"
```

## Tools

The **Smear Generator** menu contains one main workflow window and three supporting tools.

| Menu item | Purpose | When to use it |
|---|---|---|
| **Open Smear Generator** | Preview and bake 3D animation into high-resolution or pixel-art frame output. | Use this for the normal animation pipeline. |
| **FBX Avatar Setup** | Prepare a character FBX and animation FBX for humanoid retargeting. | Use this when the clip comes from another humanoid model or preview reports an avatar mismatch. |
| **FBX Texture Fixer** | Extract embedded PNG textures from an FBX into the `.fbm` folder Unity expects. | Use this when an imported character is white or its material textures are missing. |
| **Utilities > Import Exported Pixel Art Animation** | Rebuild Unity sprite, clip, controller, and prefab assets from pixel-art output exported by this generator. | Use this only when moving generated pixel-art animation to another Unity project or device. |

## 1. Smear Generator window

Open **Smear Generator > Open Smear Generator**.

The window has three pipeline modes:

### Full

Runs both offline workflows in sequence:

```text
3D character + animation
    -> velocity extraction
    -> smear generation
    -> high-resolution capture
    -> pixel-art conversion
    -> sprite package export
```

Choose **Full** when starting with a 3D character and wanting a ready-to-use pixel animation prefab.

### Smear Bake

Runs the 3D side of the pipeline and produces high-resolution frames plus metadata. It does not run pixel-art conversion.

Choose **Smear Bake** when evaluating smear output, tuning capture settings, or saving high-resolution frames for later processing.

### Pixel Art

Loads previously captured high-resolution PNG and JSON files, then runs pixelization and sprite package export without sampling the 3D character again.

Choose **Pixel Art** when adjusting palette, resolution, outline, or other pixel settings while reusing an earlier capture.

## Basic bake workflow

1. Open **Smear Generator > Open Smear Generator**.
2. Select **Full**.
3. Assign the character model.
4. Assign the animation clip.
5. Set the target FPS.
6. Expand the pixel and smear parameter sections as needed.
7. Select **Preview animation** to check pose, framing, ground position, and textures.
8. Select **Run pipeline**.
9. Review the input, smear, and pixel-art frames.
10. Open **Results** to locate the generated prefab or export the folder.

Generated assets remain inside the current Unity project until **Export Folder** is selected.

## 2. FBX Avatar Setup

Use **Smear Generator > FBX Avatar Setup** when a character and clip come from different humanoid FBX files.

A common example is:

```text
Character FBX: James_Base.fbx
Animation FBX: Mixamo_XBot_SpinKick.fbx
```

Both FBXs must have valid humanoid avatars for Unity to retarget the animation. The setup tool prepares and validates the pair before preview or bake.

Use it when:

- the animation plays on its source model but not the selected character;
- preview reports that the character has no valid humanoid avatar;
- a generic clip has no transform paths matching the character; or
- the character pose is distorted after assigning a clip from another model.

After setup, return to the main window, reassign the character and clip if needed, then preview again.

## 3. FBX Texture Fixer

Use **Smear Generator > FBX Texture Fixer** when a model imports as white or its textures are absent.

Some FBX files embed PNG data but Unity does not create or reconnect the expected texture files. The fixer extracts those PNGs into an `.fbm` folder beside the FBX and refreshes the importer.

Workflow:

1. Open **FBX Texture Fixer**.
2. Assign the affected FBX.
3. Check the proposed folder name and destination.
4. Select **Fix Texture**.
5. Reopen or reimport the model if Unity has not refreshed its preview.
6. Preview the animation in the main window.

This tool repairs FBX texture extraction. It does not pixelize a model or import generated sprite animation.

## 4. Import Exported Pixel Art Animation

**Import Exported Pixel Art Animation only accepts pixel-art animation output from Smear Generator.** It does not import high-resolution capture output, an FBX, or a normal Unity animation clip.

### Why the importer exists

A generated package contains Unity `.anim`, `.controller`, and `.prefab` files. Those assets refer to the original project's sprite and animation GUIDs. Copying them into another project's `Assets` folder without their original metadata can leave the prefab with missing sprites or a disconnected animation.

The portable folder's stable contract is its PNG and JSON data. The importer reads that data and creates new Unity assets with references belonging to the destination project.

### Source project

After a successful Full or Pixel Art run:

1. Open **Results**.
2. Select **Export Folder**.
3. Choose a folder outside the Unity project.
4. Copy that exported folder to the other device or project.

An exported folder contains files similar to:

```text
walk/
├── walk.png
├── animation.json
├── package.json
├── walk_2d.anim
├── walk_2d.controller
└── walk_2d.prefab
```

### Destination project

1. Install this generator package.
2. Choose **Smear Generator > Utilities > Import Exported Pixel Art Animation**.
3. Select the exported folder containing `package.json` and `animation.json`.
4. Unity rebuilds the sprite sheet, clip, controller, and prefab.
5. Use the rebuilt prefab from:

```text
Assets/SmearGenerator.Generated/ImportedPackages/<package-name>/
```

The importer uses the PNG and JSON as the source of truth. Do not drag the copied prefab directly into `Assets` when its sprite references are missing.

## Exported prefab usage

Drag the generated or imported `_2d.prefab` into a scene. The prefab contains:

- a `SpriteRenderer` with the first generated frame;
- an `Animator` with the generated controller; and
- an animation clip whose sprite keys follow the exported frame timing.

The generated controller starts in the generated animation state. Looping follows the metadata written during export.

## Configuration

The main window exposes configuration for:

- capture resolution and camera angle;
- animation sampling FPS;
- smear activation and effect types;
- palette size;
- pixel-art output dimensions;
- temporal coherence;
- outline settings; and
- output naming and export location.

Use preview before a full run. Framing, retargeting, and missing material textures are easier to correct before the pipeline writes every frame.

## Troubleshooting

### Character is solid white

The source FBX materials probably have no albedo texture binding. Open **FBX Texture Fixer**, repair the selected FBX, and preview again.

### Character or animation is distorted

Open **FBX Avatar Setup** and prepare the character/clip pair as humanoid assets. Confirm that both FBXs report valid humanoid data.

### Imported prefab has missing sprites

Delete the broken imported folder and run **Utilities > Import Exported Pixel Art Animation** again using the exported folder. Do not install the copied `.prefab` by itself.

### Pixel Art mode has no input

Pixel Art mode needs a matching high-resolution PNG and JSON produced by Smear Bake or Full mode. Select those files before running the pixel workflow.

### Package update does not appear

Change the Git tag in `Packages/manifest.json` when a newer release is available. Commit `Packages/manifest.json` and `Packages/packages-lock.json` together in the consuming project.

## Development layout

The repository root is the UPM package root:

```text
Editor/          editor windows, pipeline stages, import, and export
Runtime/         components required by generated runtime assets
Shaders/         capture, ghost, and motion-line shaders
Tests/Editor/    package EditMode tests
Documentation~/  package documentation
package.json     UPM package manifest
```

Package-generated files belong under `Assets/SmearGenerator.Generated/` in the consuming project, not inside the package repository.

## More documentation

- [Getting started](Documentation~/getting-started.md)
- [Dependencies](Documentation~/dependencies.md)
