using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class HighResCaptureStageDeclarationTests
    {
        [Test]
        public void HasNoRequiredInputs()
        {
            var stage = new HighResCaptureStage();
            Assert.AreEqual(0, stage.InputKey.Count);
        }

        [Test]
        public void ProducesFramesHighres()
        {
            var stage = new HighResCaptureStage();
            Assert.IsTrue(stage.OutputKey.Any(k => k.Key == "frames_highres" && k.Type == typeof(RawFrameData)));
        }

        [Test]
        public void RenderSmearFrame_CompositesAdditionalGeometryIntoHighResFrame()
        {
            GameObject target = null;
            Material material = null;
            Texture2D frameTex = null;
            Texture2D maskTex = null;

            try
            {
                target = CreateSkinnedQuad(out material);
                var ctx = CreateCaptureContext(target);
                new HighResCaptureStage().Execute(ctx);

                var raw = ctx.Get<RawFrameData>("frames_highres");
                frameTex = raw.Frames[0];
                maskTex = raw.SmearMasks[0];

                Assert.IsTrue(ContainsGreenPixel(frameTex), "high-res frame should include extra smear geometry tinted by the source material");
                Assert.IsTrue(ContainsGreenPixel(maskTex), "smear mask should preserve the source-tinted exterior smear pixels");
                Assert.IsFalse(HasOpaquePixelAtCenter(maskTex), "smear mask should not flood the base body silhouette");
            }
            finally
            {
                if (frameTex != null) Object.DestroyImmediate(frameTex);
                if (maskTex != null) Object.DestroyImmediate(maskTex);
                if (material != null) Object.DestroyImmediate(material);
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void RenderSmearFrame_UsesSourceMaterialColorForGhost()
        {
            GameObject target = null;
            Material material = null;
            Texture2D frameTex = null;

            try
            {
                target = CreateSkinnedQuad(out material, Color.green);
                var ctx = CreateCaptureContext(target);
                new HighResCaptureStage().Execute(ctx);

                var raw = ctx.Get<RawFrameData>("frames_highres");
                frameTex = raw.Frames[0];
                Assert.IsTrue(ContainsGreenPixel(frameTex),
                    "ghost should follow the source material color instead of forcing a white tint");
            }
            finally
            {
                if (frameTex != null) Object.DestroyImmediate(frameTex);
                if (material != null) Object.DestroyImmediate(material);
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        // Creates a one-bone skinned quad with a configurable source material color.
        private GameObject CreateSkinnedQuad(out Material material, Color bodyColor)
        {
            var root = new GameObject("capture_test_target");
            var bone = new GameObject("bone");
            bone.transform.SetParent(root.transform, false);

            var mesh = new Mesh { name = "capture_test_mesh" };
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

            material = CreateMaterial(bodyColor);
            smr.sharedMaterial = material;
            return root;
        }

        private GameObject CreateSkinnedQuad(out Material material)
        {
            return CreateSkinnedQuad(out material, Color.green);
        }

        // Creates a material with a color property the capture shader can copy.
        private Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        // Builds the minimum pipeline context needed by HighResCaptureStage.
        private PipelineContext CreateCaptureContext(GameObject target)
        {
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var clip = new AnimationClip { frameRate = 12f };
            var ctx = new PipelineContext(config, target, clip);

            var motion = new MotionData(1, 1, 4, 12f);
            motion.SetSkeleton(new[] { -1 }, Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 4).ToArray());
            for (int v = 0; v < 4; v++)
                motion.Vertices[0][v] = new VertexSnapshot { position = target.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices[v] };

            var smear = new SmearFrameData(1, 4);
            smear.HasSmear[0] = true;
            for (int v = 0; v < 4; v++)
                smear.DeformedPositions[0][v] = motion.Vertices[0][v].position;
            smear.AdditionalGeometry[0] = CreateWhiteSmearQuad();

            ctx.Set("motion", motion);
            ctx.Set("smear_data", smear);
            return ctx;
        }

        // Creates a camera-facing extra quad that overlaps the body and extends outside it.
        private Mesh CreateWhiteSmearQuad()
        {
            var mesh = new Mesh { name = "capture_test_smear" };
            mesh.vertices = new[]
            {
                new Vector3(0.10f, -0.15f, 0.05f),
                new Vector3(0.10f,  0.15f, 0.05f),
                new Vector3(0.75f, -0.15f, 0.05f),
                new Vector3(0.75f,  0.15f, 0.05f),
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
            mesh.colors = Enumerable.Repeat(Color.white, 4).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Checks for the green source-tinted ghost material without counting transparent background.
        private bool ContainsGreenPixel(Texture2D tex)
        {
            if (tex == null) return false;
            var pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a > 20 && p.g > 150 && p.g > p.r + 40 && p.g > p.b + 40)
                    return true;
            }
            return false;
        }

        // Checks the central body region for opaque mask pixels.
        private bool HasOpaquePixelAtCenter(Texture2D tex)
        {
            if (tex == null) return false;
            var p = tex.GetPixel(tex.width / 2, tex.height / 2);
            return p.a > 20;
        }
    }
}
