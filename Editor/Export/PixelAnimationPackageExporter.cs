using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Editor
{
    // Writes a runtime-facing pixel package and imports it back into Unity as sprites + clip + prefab.
    public static class PixelAnimationPackageExporter
    {
        public class Result
        {
            public string PackageFolder;
            public string SpriteSheetPath;
            public string AnimationJsonPath;
            public string PackageJsonPath;
            public string ClipPath;
            public string ControllerPath;
            public string PrefabPath;
        }

        // Export the package to disk, then import the sprite sheet into a ready-to-drop prefab.
        public static Result Export(
            string outputRoot, string characterName, string clipName, Texture2D sheet, SpriteSheetMetadata animationMeta)
        {
            return ExportNamedPackage(outputRoot, null, null, sheet, animationMeta);
        }

        // Export to the standard nested folder or to a caller-named direct package folder.
        public static Result ExportNamedPackage(
            string outputRoot, string packageFolderName, string assetBaseName, Texture2D sheet, SpriteSheetMetadata animationMeta)
        {
            string packageFolder = string.IsNullOrWhiteSpace(packageFolderName)
                ? BuildPackageFolder(outputRoot, animationMeta.characterName, animationMeta.clipName, "pixel")
                : CombineAssetPath(outputRoot, OutputNameUtility.SanitizeSegment(packageFolderName, "pixel"));
            AssetFolderUtility.EnsureAssetFolder(packageFolder);

            string baseName = string.IsNullOrEmpty(assetBaseName)
                ? OutputNameUtility.BuildBaseName(animationMeta.characterName, animationMeta.clipName)
                : assetBaseName;
            string spriteSheetPath = CombineAssetPath(packageFolder, animationMeta.sheetFile);
            string animationJsonPath = CombineAssetPath(packageFolder, "animation.json");
            string packageJsonPath = CombineAssetPath(packageFolder, "package.json");
            string clipPath = CombineAssetPath(packageFolder, baseName + "_2d.anim");
            string controllerPath = CombineAssetPath(packageFolder, baseName + "_2d.controller");
            string prefabPath = CombineAssetPath(packageFolder, baseName + "_2d.prefab");

            File.WriteAllBytes(spriteSheetPath, sheet.EncodeToPNG());
            File.WriteAllText(animationJsonPath, JsonUtility.ToJson(animationMeta, true));
            File.WriteAllText(packageJsonPath, JsonUtility.ToJson(
                BuildManifest(animationMeta, baseName + "_2d.anim", baseName + "_2d.controller", baseName + "_2d.prefab"), true));

            AssetDatabase.Refresh();
            ConfigureSpriteTexture(spriteSheetPath, animationMeta);

            var sprites = LoadSprites(spriteSheetPath);
            if (sprites.Length == 0)
                throw new InvalidOperationException("sprite package import created no sprites: " + spriteSheetPath);

            var clip = BuildClip(sprites, animationMeta, Path.GetFileNameWithoutExtension(clipPath));
            CreateOrReplaceAsset(clip, clipPath);
            var controller = BuildController(controllerPath, clip);
            BuildPrefab(prefabPath, baseName, sprites[0], controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new Result
            {
                PackageFolder = packageFolder,
                SpriteSheetPath = spriteSheetPath,
                AnimationJsonPath = animationJsonPath,
                PackageJsonPath = packageJsonPath,
                ClipPath = clipPath,
                ControllerPath = controllerPath,
                PrefabPath = prefabPath
            };
        }

        // Build the package folder path under the chosen output root.
        public static string BuildPackageFolder(string outputRoot, string characterName, string clipName, string variant)
        {
            string characterPart = OutputNameUtility.SanitizeSegment(characterName, "smear");
            string clipPart = OutputNameUtility.SanitizeSegment(clipName, "unnamed");
            string variantPart = OutputNameUtility.SanitizeSegment(variant, "pixel");
            return CombineAssetPath(outputRoot, characterPart, clipPart, variantPart);
        }

        // Configure texture importer settings and sprite slicing from animation metadata.
        public static void ConfigureSpriteTexture(string assetPath, SpriteSheetMetadata meta)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("texture importer missing for " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Mathf.Max(1, meta.pixelsPerUnit);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritesheet = BuildSpriteMeta(meta);
            importer.SaveAndReimport();
        }

        // Convert top-left JSON frame rects into Unity's bottom-left sprite rects.
        static SpriteMetaData[] BuildSpriteMeta(SpriteSheetMetadata meta)
        {
            var sprites = new SpriteMetaData[meta.frames.Length];
            for (int i = 0; i < meta.frames.Length; i++)
            {
                var frame = meta.frames[i];
                sprites[i] = new SpriteMetaData
                {
                    name = string.IsNullOrEmpty(frame.spriteName) ? $"frame_{frame.index:D3}" : frame.spriteName,
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(meta.pivotX, meta.pivotY),
                    rect = new Rect(
                        frame.x,
                        meta.sheetHeight - frame.y - frame.height,
                        frame.width,
                        frame.height)
                };
            }
            return sprites;
        }

        // Load imported sprites back from the generated sprite sheet.
        static Sprite[] LoadSprites(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
        }

        // Build a Mecanim-friendly clip that swaps SpriteRenderer.sprite at the baked frame rate.
        static AnimationClip BuildClip(Sprite[] sprites, SpriteSheetMetadata meta, string clipAssetName)
        {
            var clip = new AnimationClip
            {
                frameRate = Mathf.Max(1, meta.fps),
                name = string.IsNullOrEmpty(clipAssetName) ? "sprite_clip" : clipAssetName
            };

            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / Mathf.Max(1f, meta.fps),
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var so = new SerializedObject(clip);
            var loopProp = so.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopProp != null)
            {
                loopProp.boolValue = meta.loopPlayback;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return clip;
        }

        // Build a one-state AnimatorController that plays the sprite clip on loop by default.
        static AnimatorController BuildController(string controllerPath, AnimationClip clip)
        {
            DeleteIfExists(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Play");
            state.motion = clip;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // Build a prefab with SpriteRenderer + Animator so the result plays as a normal Mecanim asset.
        static void BuildPrefab(string prefabPath, string baseName, Sprite firstSprite, RuntimeAnimatorController controller)
        {
            var root = new GameObject(baseName + "_Pixel2D");
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = firstSprite;

                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // Replace an existing asset file so repeated exports stay deterministic.
        static void CreateOrReplaceAsset(UnityEngine.Object asset, string assetPath)
        {
            DeleteIfExists(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        // Delete an existing asset path before re-creating it.
        static void DeleteIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        // Create the package manifest with the generated asset filenames.
        static PixelAnimationPackageManifest BuildManifest(
            SpriteSheetMetadata meta, string clipAssetFile, string controllerAssetFile, string prefabAssetFile)
        {
            return new PixelAnimationPackageManifest
            {
                schema_version = 1,
                packageType = "smear_pixel_animation",
                characterName = meta.characterName,
                clipName = meta.clipName,
                outputMode = meta.outputMode,
                spriteSheetFile = meta.sheetFile,
                animationFile = "animation.json",
                clipAssetFile = clipAssetFile,
                controllerAssetFile = controllerAssetFile,
                prefabAssetFile = prefabAssetFile,
                generatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };
        }


        // Join asset path segments using Unity-style separators.
        static string CombineAssetPath(params string[] parts)
        {
            return string.Join("/", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(NormalizeAssetPath));
        }

        // Convert a Unity asset path into a filesystem path rooted at the project.
        static string AssetPathToSystemPath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, NormalizeAssetPath(assetPath));
        }

        // Normalize path separators so Unity APIs and file IO agree.
        static string NormalizeAssetPath(string path)
        {
            return path.Replace("\\", "/").TrimEnd('/');
        }
    }
}
