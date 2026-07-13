# Getting started

## Requirements

Use Unity `6000.3` or newer in the Unity 6.3 line. The verified development version is Unity `6000.3.6f1`.

The core package does not require extra Unity packages. See [Dependencies](dependencies.md) for details.

## Install from disk

1. Open **Window > Package Management > Package Manager** in the target Unity project.
2. Select the plus button.
3. Choose **Add package from disk**.
4. Select the standalone Smear Generator repository's root `package.json`.

## Install by manifest

Add this entry to the `dependencies` object in the target project's `Packages/manifest.json`:

```json
"com.davis.smear-generator": "file:/absolute/path/to/3D-to-Pixel-Art-Smear-Generator"
```

Replace the example with the absolute path to the standalone `3D-to-Pixel-Art-Smear-Generator` repository. Keep the `file:` prefix.

## Install from GitHub

After a release is tagged, select **Add package from git URL** in Package Manager and enter:

```text
https://github.com/Loxenary/3D-to-Pixel-Art-Smear-Generator.git#v0.2.2
```

The same dependency can be written directly in the target project's `Packages/manifest.json`:

```json
"com.davis.smear-generator": "https://github.com/Loxenary/3D-to-Pixel-Art-Smear-Generator.git#v0.2.2"
```

To update, replace the old tag with the new release tag. Commit the resulting `manifest.json` and `packages-lock.json` changes together.

## Open the tool

Choose **Smear Generator > Open Smear Generator** from the Unity menu.

## Prepare a humanoid FBX

Choose **Smear Generator > FBX Avatar Setup** when a humanoid source model or animation clip needs retarget setup. Apply the avatar settings before you bake the animation.

## Bake an animation

1. Open **Smear Generator > Open Smear Generator**.
2. Choose the character model.
3. Choose the animation clip.
4. Choose the capture, pixelization, and smear configuration assets.
5. Run the pipeline.
6. Review the generated animation in **Results**.

Generated output remains under `Assets/SmearGenerator.Generated/` in the Unity project.

## Export a generated pixel animation package

1. Complete a bake and open **Results**.
2. Choose **Export Folder**.
3. Select an external destination.
4. Copy the exported folder to the destination device or project.

Treat the exported folder as portable animation data. Install it through the Smear Generator importer so Unity rebuilds its local asset references.

## Import on another device or project

1. Install `com.davis.smear-generator` in the destination Unity project.
2. Choose **Smear Generator > Utilities > Import Exported Pixel Art Animation**.
3. Select the exported pixel animation folder.
4. Wait for Unity to import and rebuild the assets.
5. Use the rebuilt prefab from `Assets/SmearGenerator.Generated/ImportedPackages/<name>/`.

The imported folder contains a local sprite sheet, animation clip, animator controller, and prefab that reference assets in the destination project.

## Troubleshooting

### The imported prefab has missing sprites

Import the exported folder again through **Smear Generator > Utilities > Import Exported Pixel Art Animation**. Do not drag a copied `.prefab` from an external folder into `Assets/`; its references can point to GUIDs from the source project.

