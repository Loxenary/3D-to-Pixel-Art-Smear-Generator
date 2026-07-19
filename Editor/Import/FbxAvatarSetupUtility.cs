using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    /// <summary>Applies common humanoid avatar import settings to FBX assets.</summary>
    public static class FbxAvatarSetupUtility
    {
        // Turn an FBX into a humanoid character that creates its own avatar.
        public static SetupResult MakeHumanoidFromModel(string assetPath)
        {
            var importer = GetImporter(assetPath);
            if (importer == null)
                return SetupResult.Fail("Select a raw .fbx asset first.");

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();

            return ReadBackStatus(assetPath, "Humanoid avatar created from model.");
        }

        // Prepare a character and clip FBX for Mecanim retargeting.
        public static SetupResult PrepareHumanoidRetargetPair(string characterAssetPath, string clipAssetPath)
        {
            if (GetImporter(characterAssetPath) == null || GetImporter(clipAssetPath) == null)
                return SetupResult.Fail("Select raw .fbx assets for both Character Fbx and Clip Fbx.");

            if (NeedsHumanoidCreateFromModel(characterAssetPath))
            {
                var result = MakeHumanoidFromModel(characterAssetPath);
                if (!result.Success)
                    return result;
            }

            if (NeedsHumanoidCreateFromModel(clipAssetPath))
            {
                var result = MakeHumanoidFromModel(clipAssetPath);
                if (!result.Success)
                    return result;
            }

            if (HasValidHumanoidAvatar(characterAssetPath) && HasHumanoidMotionClip(clipAssetPath))
                return SetupResult.Ok("Retarget pair ready. Character and clip are humanoid.");

            return SetupResult.Fail("Retarget pair is still not ready. Character needs a valid humanoid avatar and clip needs humanoid motion.");
        }

        // Turn an FBX into a humanoid asset that copies another model's avatar.
        public static SetupResult CopyHumanoidAvatar(string assetPath, GameObject avatarSource)
        {
            var importer = GetImporter(assetPath);
            if (importer == null)
                return SetupResult.Fail("Select a raw .fbx asset first.");

            var sourceAvatar = ResolveAvatar(avatarSource);
            if (sourceAvatar == null)
                return SetupResult.Fail("Avatar Source needs an FBX/model with a valid humanoid avatar.");

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.SaveAndReimport();

            return ReadBackStatus(assetPath, "Humanoid avatar copied from source.");
        }

        // Put an FBX back on the basic generic rig path.
        public static SetupResult MakeGeneric(string assetPath)
        {
            var importer = GetImporter(assetPath);
            if (importer == null)
                return SetupResult.Fail("Select a raw .fbx asset first.");

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();

            return ReadBackStatus(assetPath, "Rig set back to Generic.");
        }

        // Resolve an object field selection to a direct FBX asset path.
        public static string ResolveFbxAssetPath(Object selected)
        {
            if (selected == null) return null;
            string path = AssetDatabase.GetAssetPath(selected);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return path;
            return null;
        }

        // User-facing status for a single FBX field -- returns null when path is empty.
        public static string DescribeForUser(string assetPath)
        {
            var importer = GetImporter(assetPath);
            if (importer == null)
                return null;
            bool isHumanoid = importer.animationType == ModelImporterAnimationType.Human;
            bool hasAvatar  = HasValidHumanoidAvatar(assetPath);
            if (isHumanoid && hasAvatar)
                return "Already humanoid with a valid avatar.";
            if (isHumanoid)
                return "Humanoid rig but no valid avatar yet -- Prepare Retarget Pair will reimport it.";
            return "Generic rig -- Prepare Retarget Pair will convert it to humanoid.";
        }

        // Build a short status string for the current importer state (internal / debug use).
        public static string Describe(string assetPath)
        {
            var importer = GetImporter(assetPath);
            if (importer == null)
                return "Drop a raw .fbx asset.";
            var prefab   = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            var avatar   = animator != null ? animator.avatar : null;
            string avatarLabel = avatar == null ? "none" : $"valid={avatar.isValid}, human={avatar.isHuman}";
            return $"Rig: {importer.animationType} | Avatar Setup: {importer.avatarSetup} | Avatar: {avatarLabel}";
        }

        // Load the model importer when the selection is a valid FBX asset.
        static ModelImporter GetImporter(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return null;
            return AssetImporter.GetAtPath(assetPath) as ModelImporter;
        }

        // Check whether the FBX still needs humanoid create-from-model import settings.
        static bool NeedsHumanoidCreateFromModel(string assetPath)
        {
            var importer = GetImporter(assetPath);
            return importer == null
                || importer.animationType != ModelImporterAnimationType.Human
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || !HasValidHumanoidAvatar(assetPath);
        }

        // Public accessor for UI windows to check avatar state without reimporting.
        public static bool HasValidHumanoidAvatarPublic(string assetPath) => HasValidHumanoidAvatar(assetPath);

        // Check whether the FBX prefab exposes a valid humanoid avatar.
        static bool HasValidHumanoidAvatar(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            var avatar = animator != null ? animator.avatar : null;
            return avatar != null && avatar.isValid && avatar.isHuman;
        }

        // Check whether the FBX contains at least one humanoid motion clip.
        static bool HasHumanoidMotionClip(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.humanMotion)
                    return true;
            }
            return false;
        }

        // Pull a usable avatar from a source FBX/model.
        static Avatar ResolveAvatar(GameObject avatarSource)
        {
            if (avatarSource == null)
                return null;
            var animator = avatarSource.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null)
                return null;
            return animator.avatar;
        }

        // Read back the importer + avatar state after Unity reimports the asset.
        static SetupResult ReadBackStatus(string assetPath, string prefix)
        {
            bool hasAvatar = HasValidHumanoidAvatar(assetPath);
            string suffix  = hasAvatar ? "Avatar is valid and humanoid." : "Avatar not confirmed -- try re-importing manually if the clip doesn't play.";
            return SetupResult.Ok($"{prefix} {suffix}");
        }

        public sealed class SetupResult
        {
            public bool Success;
            public string Message;

            // Build a success result.
            public static SetupResult Ok(string message)
            {
                return new SetupResult { Success = true, Message = message };
            }

            // Build a failure result.
            public static SetupResult Fail(string message)
            {
                return new SetupResult { Success = false, Message = message };
            }
        }
    }
}
