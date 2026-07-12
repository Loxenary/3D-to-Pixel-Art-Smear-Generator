# 3D-to-Pixel-Art Smear Framework

A Unity editor package for converting 3D character animation into pixel-art sprite animation with smear frames.

## Requirements

Use Unity `6000.3` or newer in the Unity 6.3 line. Development is verified with Unity `6000.3.6f1`. The core package has no dependencies beyond Unity built-in editor and runtime modules.

See [Dependencies](Documentation~/dependencies.md) for package and test dependency details.

## Install

In Unity Package Manager, select the plus button, choose **Add package from disk**, and select this package's `package.json`.

For local manifest installation, add the following entry to the target project's `Packages/manifest.json`:

```json
"com.davis.smear-framework": "file:/absolute/path/to/Smear-Framework"
```

For a tagged GitHub release, use the repository URL and version tag:

```json
"com.davis.smear-framework": "https://github.com/Loxenary/3D-to-Pixel-Art-Smear-Framework.git#v0.1.0"
```

Change the tag when upgrading, then commit both `Packages/manifest.json` and `Packages/packages-lock.json` in the consuming project.

## Start baking

Open **Smear Framework > Open Smear Framework**. Choose a character, animation clip, and configuration assets, then run the pipeline.

Use **Smear Framework > FBX Avatar Setup** when a humanoid source or clip needs retarget setup.

## Move generated animation to another project

In **Results**, choose **Export Folder**. In the destination project, install this package and choose **Smear Framework > Import Pixel Animation Package**. Select the exported folder, then use the rebuilt prefab under `Assets/SmearFramework.Generated/ImportedPackages/<name>/`.

Read [Getting started](Documentation~/getting-started.md) for the full setup, bake, export, import, and troubleshooting steps.
