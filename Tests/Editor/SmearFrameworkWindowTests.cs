using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.Editor;

namespace SmearFramework.Tests
{
    public class SmearFrameworkWindowTests
    {
        // Copies the latest sprite-sheet result into the window state.
        [Test]
        public void UpdateResultArtifacts_CopiesLatestSpriteSheetResult()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var target = new GameObject("t");

            try
            {
                var ctx = new PipelineContext(config, target, null);
                var result = new SpriteSheetResult
                {
                    PngPath = "Assets/out/sheet.png",
                    JsonPath = "Assets/out/sheet.json",
                    PrefabPath = "Assets/out/sheet.prefab",
                    PackageFolder = "Assets/out"
                };
                var frame = new CaptureFrame { Resolution = 64 };
                ctx.Set("sprite_sheet", result);
                ctx.Set("capture_frame", frame);

                InvokePrivate(window, "UpdateResultArtifacts", ctx);

                Assert.AreSame(result, GetPrivateField<SpriteSheetResult>(window, "_lastResult"));
                Assert.AreSame(frame, GetPrivateField<CaptureFrame>(window, "_lastCaptureFrame"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Clears stale output state when the latest pipeline run produced no sprite sheet.
        [Test]
        public void UpdateResultArtifacts_ClearsStaleSpriteSheetResult()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var target = new GameObject("t");

            try
            {
                SetPrivateField(window, "_lastResult", new SpriteSheetResult { PngPath = "stale.png" });
                SetPrivateField(window, "_lastCaptureFrame", new CaptureFrame { Resolution = 32 });

                var ctx = new PipelineContext(config, target, null);
                InvokePrivate(window, "UpdateResultArtifacts", ctx);

                Assert.IsNull(GetPrivateField<SpriteSheetResult>(window, "_lastResult"));
                Assert.IsNull(GetPrivateField<CaptureFrame>(window, "_lastCaptureFrame"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Keeps the post-bake status focused on completion instead of internal Unity assets.
        [Test]
        public void BuildSpriteSheetStatus_OmitsInternalAssetDetails()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var target = new GameObject("t");

            try
            {
                var ctx = new PipelineContext(config, target, null);
                ctx.Set("sprite_sheet", new SpriteSheetResult
                {
                    PngPath = "Assets/out/sheet.png",
                    PrefabPath = "Assets/out/sheet.prefab",
                    PackageFolder = "Assets/out"
                });

                string status = InvokePrivate<string>(window, "BuildSpriteSheetStatus", ctx, 7259f, "");
                Assert.AreEqual("Pixel art done - 7259ms.", status);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Defaults the full-mode comparison to pixel frames when both sources exist.
        [Test]
        public void ResolveLeftComparisonStage_DefaultsToPixelatedWhenBothSourcesExist()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();

            try
            {
                object value = InvokePrivate<object>(window, "ResolveLeftComparisonStage", true, true, true);
                Assert.AreEqual("Pixelated", value.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Falls back to the 3D source when the pixel comparison frames are missing.
        [Test]
        public void ResolveLeftComparisonStage_FallsBackToSource3DWhenPixelMissing()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();

            try
            {
                var field = window.GetType().GetField("_leftComparisonStage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                    throw new InvalidOperationException("Field '_leftComparisonStage' not found on SmearFrameworkWindow");
                object pixelated = Enum.Parse(field.FieldType, "Pixelated");
                SetPrivateField(window, "_leftComparisonStage", pixelated);

                object value = InvokePrivate<object>(window, "ResolveLeftComparisonStage", true, true, false);
                Assert.AreEqual("Source3D", value.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Keeps the left preview populated for pixel-only runs by reusing the captured high-res frames.
        [Test]
        public void SetInputPreviewFrames_UsesFramesHighResAsLeftPreview()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var target = new GameObject("t");
            var frame = new Texture2D(2, 2);
            var frames = new[] { frame };
        
            try
            {
                var ctx = new PipelineContext(config, target, null);
                var raw = new RawFrameData(1, 2, 2);
                SetPrivateField(raw, "_frames", frames);
                ctx.Set("frames_highres", raw);
        
                InvokePrivate(window, "SetInputPreviewFrames", ctx);
        
                Assert.AreSame(frames, GetPrivateField<Texture2D[]>(window, "_clean3DFrames"));
                Assert.IsNull(GetPrivateField<Texture2D[]>(window, "_cleanPixelFrames"));
                Assert.AreSame(frames, GetPrivateField<Texture2D[]>(window, "_cleanFrames"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
        // Labels the left pane as input when pixel art compares against the raw source frames.
        [Test]
        public void ResolveLeftPaneLabel_ReturnsInputForPixelOnlyPreview()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();

            try
            {
                string value = InvokePrivate<string>(window, "ResolveLeftPaneLabel", false, true, false, false, true);
                Assert.AreEqual("Input", value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Keeps the overlay preset label honest when the current angle is not one of the known presets.
        [Test]
        public void CurrentCapturePresetName_ReturnsCustomForNonPresetAngle()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();

            try
            {
                SetPrivateField(window, "_captureCameraEuler", new Vector3(12f, 34f, 0f));
                string value = InvokePrivate<string>(window, "CurrentCapturePresetName");
                Assert.AreEqual("Custom", value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Confirms a temporary 3D result still shows the Results panel.
        [Test]
        public void HasResultsPanel_ReturnsTrueForTemporarySmear3DResult()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();

            try
            {
                var result = new SmearScene3DExporter.Result { PrefabPath = SmearFrameworkPaths.Smear3DTempOutput + "/Runner/Runner_smear3D.prefab" };
                SetPrivateField(window, "_lastSmear3DResult", result);
                SetPrivateField(window, "_lastSmear3DResultIsTemporary", true);

                bool hasPanel = InvokePrivate<bool>(window, "HasResultsPanel");
                Assert.IsTrue(hasPanel);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        // Confirms that a non-Assets folder is rejected before inspecting cached data.
        [Test]
        public void CanExportLatestSmear3DResult_RejectsNonAssetsFolder()
        {
            var window = ScriptableObject.CreateInstance<SmearFrameworkWindow>();

            try
            {
                SetPrivateField(window, "_smear3DExportFolder", "/tmp/out");

                bool canExport = InvokePrivate<bool>(window, "CanExportLatestSmear3DResult");
                Assert.IsFalse(canExport);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }


        // Runs a private window helper through reflection and returns its value when needed.
        static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            var m = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (m == null)
                throw new InvalidOperationException($"Method '{methodName}' not found on {target.GetType().Name}");
            return (T)m.Invoke(target, args);
        }

        // Runs a private window helper through reflection.
        static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var m = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (m == null)
                throw new InvalidOperationException($"Method '{methodName}' not found on {target.GetType().Name}");
            m.Invoke(target, args);
        }

        // Reads a private field for regression assertions.
        static T GetPrivateField<T>(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}");
            return (T)f.GetValue(target);
        }

        // Writes a private field so tests can start from a stale-window state.
        static void SetPrivateField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }
    }
}
