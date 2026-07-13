using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using SmearFramework.DataTypes;
using SmearFramework.SmearGeneration;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.Tests
{
    public class SmearGenerationTests
    {
        [Test]
        public void ElongatedSmear_DisplacesVerticesAlongSpline()
        {
            var ctx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0.5f);
            var output = new SmearFrameData(ctx.Get<MotionData>("motion").FrameCount, ctx.Get<MotionData>("motion").VertexCount);

            for (int f = 0; f < output.FrameCount; f++)
                for (int v = 0; v < ctx.Get<MotionData>("motion").VertexCount; v++)
                    output.DeformedPositions[f][v] = ctx.Get<MotionData>("motion").Vertices[f][v].position;

            ElongatedSmearStrategy strat = new ElongatedSmearStrategy();
            Assert.IsTrue(strat.IsEnabled(ctx.Config));

            strat.Apply(ctx, output, 2);

            var basePos = ctx.Get<MotionData>("motion").Vertices[2][0].position;
            var displaced = output.DeformedPositions[2][0];
            float dist = Vector3.Distance(basePos, displaced);

            Assert.That(dist, Is.GreaterThan(0.001f));
        }

        [Test]
        public void ElongatedSmear_ZeroOffset_NoDisplacement()
        {
            var ctx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0f);
            var output = new SmearFrameData(ctx.Get<MotionData>("motion").FrameCount, ctx.Get<MotionData>("motion").VertexCount);

            for (int f = 0; f < output.FrameCount; f++)
                for (int v = 0; v < ctx.Get<MotionData>("motion").VertexCount; v++)
                    output.DeformedPositions[f][v] = ctx.Get<MotionData>("motion").Vertices[f][v].position;

            var strat = new ElongatedSmearStrategy();
            strat.Apply(ctx, output, 2);

            Vector3 basePos = ctx.Get<MotionData>("motion").Vertices[2][0].position;
            Vector3 result = output.DeformedPositions[2][0];
            Assert.That(Vector3.Distance(basePos, result), Is.LessThan(0.001f));
        }

        [Test]
        public void MotionLines_ProducesGeometry()
        {
            var ctx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0.8f);

            var effects = ScriptableObject.CreateInstance<SmearEffectsConfig>();
            var so = new SerializedObject(effects);
            so.FindProperty("_enableMotionLines").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var cfgSo = new SerializedObject(ctx.Config);
            cfgSo.FindProperty("_smearEffects").objectReferenceValue = effects;
            cfgSo.ApplyModifiedPropertiesWithoutUndo();

            var output = new SmearFrameData(ctx.Get<MotionData>("motion").FrameCount, ctx.Get<MotionData>("motion").VertexCount);

            for (int f = 0; f < output.FrameCount; f++)
                for (int v = 0; v < ctx.Get<MotionData>("motion").VertexCount; v++)
                    output.DeformedPositions[f][v] = ctx.Get<MotionData>("motion").Vertices[f][v].position;

            var strat = new MotionLineStrategy();
            Assert.IsTrue(strat.IsEnabled(ctx.Config));
            strat.Apply(ctx, output, 2);
            Assert.IsNotNull(output.MotionLineGeometry[2]);
            Assert.That(output.MotionLineGeometry[2].vertexCount, Is.GreaterThan(0));
        }

        // Confirm seed trajectory speed can emit lines without a bone-ribbon offset
        [Test]
        public void MotionLines_UsesSpeedWhenMotionOffsetIsZero()
        {
            var ctx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0f);
            var effects = ScriptableObject.CreateInstance<SmearEffectsConfig>();
            ConfigureMotionLines(ctx.Config, effects, 5f);
            var output = CreateBaseSmearOutput(ctx);

            new MotionLineStrategy().Apply(ctx, output, 2);

            Assert.IsNotNull(output.MotionLineGeometry[2]);
            Assert.That(output.MotionLineGeometry[2].vertexCount, Is.GreaterThan(0));
        }

        // Confirm the maximum-length control changes the generated trail span
        [Test]
        public void MotionLines_MaxLengthChangesTrailBounds()
        {
            var shortCtx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0f);
            var longCtx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0f);
            var shortEffects = ScriptableObject.CreateInstance<SmearEffectsConfig>();
            var longEffects = ScriptableObject.CreateInstance<SmearEffectsConfig>();
            ConfigureMotionLines(shortCtx.Config, shortEffects, 0.5f);
            ConfigureMotionLines(longCtx.Config, longEffects, 5f);
            var shortOutput = CreateBaseSmearOutput(shortCtx);
            var longOutput = CreateBaseSmearOutput(longCtx);

            var strategy = new MotionLineStrategy();
            strategy.Apply(shortCtx, shortOutput, 2);
            strategy.Apply(longCtx, longOutput, 2);

            var shortMesh = shortOutput.MotionLineGeometry[2];
            var longMesh = longOutput.MotionLineGeometry[2];
            Assert.IsNotNull(shortMesh);
            Assert.IsNotNull(longMesh);
            shortMesh.RecalculateBounds();
            longMesh.RecalculateBounds();
            Assert.That(longMesh.bounds.size.x, Is.GreaterThan(shortMesh.bounds.size.x));
        }

        [Test]
        public void MultipleSmear_NeutralOffsets_ProducesTransparentGhostPerEq12()
        {
            GameObject target = null;
            try
            {
                target = CreateSkinnedQuad();
                var ctx = CreateTargetContext(target, velocity: new Vector3(10, 0, 0), motionOffset: 0f);
                var output = new SmearFrameData(ctx.Get<MotionData>("motion").FrameCount, ctx.Get<MotionData>("motion").VertexCount);

                for (int f = 0; f < output.FrameCount; f++)
                    for (int v = 0; v < ctx.Get<MotionData>("motion").VertexCount; v++)
                        output.DeformedPositions[f][v] = ctx.Get<MotionData>("motion").Vertices[f][v].position;

                var strat = new MultipleSmearStrategy();
                Assert.IsTrue(strat.IsEnabled(ctx.Config));
                strat.Apply(ctx, output, 2);

                var ghost = output.AdditionalGeometry[2];
                Assert.IsNotNull(ghost, "multiple smear should still emit ghost geometry");
                Assert.That(ghost.vertexCount, Is.GreaterThan(0));
                Assert.That(ghost.colors.All(c => c.a < 0.001f), Is.True,
                    "Eq 12 should fully hide a copy when no vertex passes the direction gate");
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void MultipleSmear_DisplacementScale_MovesGhostFarther()
        {
            GameObject targetA = null;
            GameObject targetB = null;
            try
            {
                targetA = CreateSkinnedQuad();
                targetB = CreateSkinnedQuad();

                var ctxA = CreateTargetContext(targetA, velocity: new Vector3(10, 0, 0), motionOffset: 0.8f);
                var ctxB = CreateTargetContext(targetB, velocity: new Vector3(10, 0, 0), motionOffset: 0.8f);
                ConfigureFutureOnlyMultiple(ctxA.Config.SmearEffects, futureDisplacement: 1f, futureOpacityFactor: 1f);
                ConfigureFutureOnlyMultiple(ctxB.Config.SmearEffects, futureDisplacement: 2f, futureOpacityFactor: 1f);

                var outputA = CreateBaseSmearOutput(ctxA);
                var outputB = CreateBaseSmearOutput(ctxB);

                var strat = new MultipleSmearStrategy();
                strat.Apply(ctxA, outputA, 2);
                strat.Apply(ctxB, outputB, 2);

                float centerA = ComputeAverageX(outputA.AdditionalGeometry[2]);
                float centerB = ComputeAverageX(outputB.AdditionalGeometry[2]);
                Assert.That(centerB, Is.GreaterThan(centerA + 0.5f),
                    "future displacement scale should move the copy farther along the trajectory");
            }
            finally
            {
                if (targetA != null) Object.DestroyImmediate(targetA);
                if (targetB != null) Object.DestroyImmediate(targetB);
            }
        }

        [Test]
        public void MultipleSmear_StageDoesNotHardCutFrameBelowBoneSpeedThreshold()
        {
            GameObject target = null;
            try
            {
                target = CreateSkinnedQuad();
                var ctx = CreateTargetContext(target, velocity: new Vector3(0.5f, 0, 0), motionOffset: 0.8f);
                ConfigureFutureOnlyMultiple(ctx.Config.SmearEffects, futureDisplacement: 1f, futureOpacityFactor: 1f);

                var stage = new Stages.SmearGenerationStage();
                stage.Execute(ctx);

                var smear = ctx.Get<SmearFrameData>("smear_data");
                Assert.IsTrue(smear.HasSmear[2], "multiple smear should not drop the whole frame just because bone speed fell below the global threshold");
                Assert.IsNotNull(smear.AdditionalGeometry[2], "multiple geometry should still be emitted when motion offsets support it");
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void MultipleSmear_OpacityFactor_FadesFartherCopies()
        {
            GameObject target = null;
            try
            {
                target = CreateSkinnedQuad();
                var ctx = CreateTargetContext(target, velocity: new Vector3(10, 0, 0), motionOffset: 0.8f);
                ConfigureFutureCopies(ctx.Config.SmearEffects, futureCopies: 2, futureDisplacement: 1f, futureOpacityFactor: 0.5f);

                var output = CreateBaseSmearOutput(ctx);
                var strat = new MultipleSmearStrategy();
                strat.Apply(ctx, output, 2);

                var colors = output.AdditionalGeometry[2].colors;
                float nearAlpha = AverageAlpha(colors, 0, 4);
                float farAlpha = AverageAlpha(colors, 4, 4);
                Assert.That(farAlpha, Is.LessThan(nearAlpha * 0.75f),
                    "farther future copies should fade out instead of stopping at full opacity");
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SmearGenerationStage_BelowThreshold_NoSmear()
        {
            var ctx = CreateTestContext(velocity: Vector3.zero, motionOffset: 0f);

            var stage = new Stages.SmearGenerationStage();
            stage.Execute(ctx);

            var smear = ctx.Get<SmearFrameData>("smear_data");
            for (int f = 0; f < smear.FrameCount; f++)
                Assert.IsFalse(smear.HasSmear[f], $"Frame {f} should have no smear");
        }

        [Test]
        public void SmearGenerationStage_AboveThreshold_HasSmear()
        {
            var ctx = CreateTestContext(velocity: new Vector3(10, 0, 0), motionOffset: 0.5f);

            var stage = new Stages.SmearGenerationStage();
            stage.Execute(ctx);

            var smear = ctx.Get<SmearFrameData>("smear_data");
            bool any = false;
            for (int f = 0; f < smear.FrameCount; f++)
                if (smear.HasSmear[f]) { any = true; break; }

            Assert.IsTrue(any, "Moving character should trigger smear on some frames");
        }

        private PipelineContext CreateTestContext(Vector3 velocity, float motionOffset)
        {
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var ctx = new PipelineContext(config, null, null);
            PopulateMotionData(ctx, null, velocity, motionOffset);
            return ctx;
        }

        private PipelineContext CreateTargetContext(GameObject target, Vector3 velocity, float motionOffset)
        {
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var effects = ScriptableObject.CreateInstance<SmearEffectsConfig>();
            var cfgSo = new SerializedObject(config);
            cfgSo.FindProperty("_smearEffects").objectReferenceValue = effects;
            cfgSo.ApplyModifiedPropertiesWithoutUndo();

            var ctx = new PipelineContext(config, target, null);
            PopulateMotionData(ctx, target.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices, velocity, motionOffset);
            return ctx;
        }

        // Configure the motion-line controls used by geometry tests
        private void ConfigureMotionLines(PipelineConfig config, SmearEffectsConfig effects, float maxLength)
        {
            var effectsSo = new SerializedObject(effects);
            effectsSo.FindProperty("_enableMotionLines").boolValue = true;
            effectsSo.FindProperty("_motionLineSpeedThreshold").floatValue = 0f;
            effectsSo.FindProperty("_motionLineSeeds").intValue = 2;
            effectsSo.FindProperty("_motionLineMaxLength").floatValue = maxLength;
            effectsSo.ApplyModifiedPropertiesWithoutUndo();

            var configSo = new SerializedObject(config);
            configSo.FindProperty("_smearEffects").objectReferenceValue = effects;
            configSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ConfigureFutureOnlyMultiple(SmearEffectsConfig effects, float futureDisplacement, float futureOpacityFactor)
        {
            ConfigureFutureCopies(effects, futureCopies: 1, futureDisplacement, futureOpacityFactor);
        }

        private void ConfigureFutureCopies(SmearEffectsConfig effects, int futureCopies, float futureDisplacement, float futureOpacityFactor)
        {
            var so = new SerializedObject(effects);
            so.FindProperty("_pastCopies").intValue = 0;
            so.FindProperty("_futureCopies").intValue = futureCopies;
            so.FindProperty("_overlapCount").intValue = 1;
            so.FindProperty("_futureDisplacement").floatValue = futureDisplacement;
            so.FindProperty("_futureOpacityFactor").floatValue = futureOpacityFactor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private SmearFrameData CreateBaseSmearOutput(PipelineContext ctx)
        {
            var motion = ctx.Get<MotionData>("motion");
            var output = new SmearFrameData(motion.FrameCount, motion.VertexCount);
            for (int f = 0; f < output.FrameCount; f++)
                for (int v = 0; v < motion.VertexCount; v++)
                    output.DeformedPositions[f][v] = motion.Vertices[f][v].position;
            return output;
        }

        private float ComputeAverageX(Mesh mesh)
        {
            float sum = 0f;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                sum += verts[i].x;
            return sum / Mathf.Max(1, verts.Length);
        }

        private float AverageAlpha(Color[] colors, int start, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += colors[start + i].a;
            return sum / Mathf.Max(1, count);
        }

        private void PopulateMotionData(PipelineContext ctx, Vector3[] baseVertices, Vector3 velocity, float motionOffset)
        {
            int frames = 5;
            int verts = baseVertices != null ? baseVertices.Length : 3;
            int bones = 1;
            float fps = 10f;

            var motion = new MotionData(frames, bones, verts, fps);
            motion.SetSkeleton(new int[] { -1 }, Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, verts).ToArray());

            for (int f = 0; f < frames; f++)
            {
                float t = f / fps;
                motion.Bones[f][0] = new BoneSnapshot
                {
                    position = velocity * t,
                    rotation = Quaternion.identity,
                    linearVelocity = velocity,
                    angularVelocity = Vector3.zero
                };

                for (int v = 0; v < verts; v++)
                {
                    Vector3 basePos = baseVertices != null ? baseVertices[v] : new Vector3(v * 0.5f, 0, 0);
                    motion.Vertices[f][v] = new VertexSnapshot
                    {
                        position = velocity * t + basePos,
                        motionOffset = motionOffset
                    };
                }
            }

            var trajBuilder = new TrajectoryBuilder();
            var traj = trajBuilder.Build(motion);
            ctx.Set("motion", motion);
            ctx.Set("trajectory", traj);
        }

        private GameObject CreateSkinnedQuad()
        {
            var root = new GameObject("multiple_smear_test_target");
            var bone = new GameObject("bone");
            bone.transform.SetParent(root.transform, false);

            var mesh = new Mesh { name = "multiple_smear_test_mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.4f, -0.4f, 0f),
                new Vector3(-0.4f,  0.4f, 0f),
                new Vector3( 0.4f, -0.4f, 0f),
                new Vector3( 0.4f,  0.4f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
            mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 4).ToArray();
            mesh.bindposes = new[] { bone.transform.worldToLocalMatrix * root.transform.localToWorldMatrix };
            mesh.RecalculateBounds();

            var smr = root.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = new[] { bone.transform };
            smr.rootBone = bone.transform;
            smr.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
            return root;
        }
    }
}
