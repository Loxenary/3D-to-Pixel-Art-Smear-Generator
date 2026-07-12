using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SmearFramework.AnimationSampling;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Stages
{
    /// <summary>Renders each animation frame to a high-res Texture2D on an isolated layer.</summary>
    [InternalStage]
    public class HighResCaptureStage : IPipelineStage
    {
        public string Name => "High-Res Capture";

        // Built lazily so the ghost shader only loads when needed.
        private static Material _ghostMaterial;
        public IReadOnlyList<ArtifactKey> InputKey => System.Array.Empty<ArtifactKey>();
        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<RawFrameData>("frames_highres"),
            ArtifactKey.Of<string>("frames_highres_disk"),
            ArtifactKey.Of<CaptureFrame>("capture_frame"),
        };

        const int CAPTURE_LAYER = 31; // isolation layer, keep clear

        // Renders the current clip into high-res frame textures.
        public void Execute(PipelineContext ctx)
        {
            var smear = ctx.Has("smear_data") ? ctx.Get<SmearFrameData>("smear_data") : null;
            var motion = ctx.Has("motion") ? ctx.Get<MotionData>("motion") : null;
            int res = ctx.Config.CaptureResolution;
            int frameCount = smear != null ? smear.FrameCount : ComputeFrameCountFromClip(ctx);

            var rawFrames = new RawFrameData(frameCount, res, res);
            SkinnedMeshRenderer[] smrs = ctx.Target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var clip = ctx.Clip;
            float dt = 1f / ctx.Config.TargetFps;

            var camGo = new GameObject("_CaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << CAPTURE_LAYER;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.enabled = false;

            var rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;


            var tempMaterials = new List<Material>();

            try
            {
                Bounds referenceBounds = ComputeReferenceBounds(ctx.Target, smrs);
                using (var poseSampler = new ClipPoseSampler(ctx.Target, clip))
                {
                    Bounds clipBounds = ComputeBounds(ctx.Target, smrs, poseSampler, frameCount, dt);
                    float referenceHalfSize = Mathf.Max(referenceBounds.extents.x, referenceBounds.extents.y);
                    float referencePadding = referenceHalfSize * 0.4f;
                    float autoOrthoSize = referenceHalfSize + referencePadding;
                    float fixedOrthoSize = ctx.Config.FixedCaptureOrthoSize;
                    Vector3 center = clipBounds.center;
                    cam.orthographicSize = fixedOrthoSize > 0f ? fixedOrthoSize : autoOrthoSize;
                    Quaternion captureRotation = Quaternion.Euler(ctx.Config.CaptureCameraEuler) *
                        Quaternion.LookRotation(-Vector3.forward, Vector3.up);
                    camGo.transform.rotation = captureRotation;
                    camGo.transform.position = center - camGo.transform.forward * 50f;
                    // no capture light -- always unlit

                    ctx.Set("capture_frame", new CaptureFrame
                    {
                        Center = center,
                        Rotation = captureRotation,
                        OrthoSize = cam.orthographicSize,
                        Resolution = res,
                        ReferencePixelHeight = referenceBounds.size.y / (2f * cam.orthographicSize) * ctx.Config.PixelHeight,
                    });

                    for (int f = 0; f < frameCount; f++)
                    {
                        poseSampler.Sample(f * dt);

                        bool hasSmear = smear != null && motion != null && smear.HasSmear[f];
                        if (hasSmear)
                        {
                            var (frame, mask) = RenderSmearFrame(smrs, smear, motion, f, cam, rt, res, true, tempMaterials);
                            rawFrames.Frames[f] = frame;
                            rawFrames.SmearMasks[f] = mask;
                        }
                        else
                            rawFrames.Frames[f] = RenderCleanFrame(smrs, cam, rt, res, true, tempMaterials);
                    }
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                foreach (var m in tempMaterials)
                    if (m != null) Object.DestroyImmediate(m);
            }

            ctx.Set("frames_highres", rawFrames);

            // Workflow 2 reads the baked sheet from disk when needed.
            if (ctx.Config.SaveHighResToDisk)
            {
                var meta = BuildHighResMetadata(ctx, rawFrames, smear);
                string folder = ctx.Config.OutputDirectory;
                string targetName = ctx.Target != null ? ctx.Target.name : "unknown";
                string clipName = ctx.Clip != null ? ctx.Clip.name : "unnamed";
                string baseName = OutputNameUtility.BuildBaseName(targetName, clipName);
                var (pngPath, _) = HighResDiskWriter.Save(folder, baseName, rawFrames, meta);
                ctx.Set("frames_highres_disk", pngPath);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
        }

        // Fills the JSON sidecar before the writer patches atlas dimensions.
        HighResMetadata BuildHighResMetadata(PipelineContext ctx, RawFrameData raw, SmearFrameData smear)
        {
            var meta = new HighResMetadata
            {
                schema_version = 1,
                prefab = OutputNameUtility.SanitizeSegment(ctx.Target != null ? ctx.Target.name : "unknown", "unknown"),
                clip = OutputNameUtility.SanitizeSegment(ctx.Clip != null ? ctx.Clip.name : "unnamed", "unnamed"),
                frame_count = raw.FrameCount,
                fps = ctx.Config.TargetFps,
            };
            if (smear != null)
            {
                meta.smear_intensity = (float[])smear.SmearIntensity.Clone();
                int smeared = 0;
                for (int f = 0; f < smear.FrameCount; f++)
                    if (smear.HasSmear[f]) smeared++;
                meta.smeared_count = smeared;
            }
            return meta;
        }

        // Falls back to clip length when no smear data is present.
        private int ComputeFrameCountFromClip(PipelineContext ctx)
        {
            float dt = 1f / ctx.Config.TargetFps;
            return Mathf.Max(1, Mathf.CeilToInt(ctx.Clip.length / dt));
        }

        // render the pose as-is, no deformation
        Texture2D RenderCleanFrame(SkinnedMeshRenderer[] smrs, Camera cam, RenderTexture rt, int res,
            bool captureUnlit, List<Material> tempMats)
        {
            var tempObjects = new GameObject[smrs.Length];
            try
            {
                for (int m = 0; m < smrs.Length; m++)
                {
                    var mesh = new Mesh();
                    smrs[m].BakeMesh(mesh);
                    mesh.RecalculateNormals();
                    tempObjects[m] = CreateTempRenderer(smrs[m], mesh, captureUnlit, tempMats);
                }

                cam.Render();
                return Readback(rt, res);
            }
            finally
            {
                for (int i = 0; i < tempObjects.Length; i++)
                    if (tempObjects[i] != null) Object.DestroyImmediate(tempObjects[i]);
            }
        }

        // apply smear displacement to verts, render the full composite, then capture overlay-only geometry for pixel color reservation.
        (Texture2D frame, Texture2D mask) RenderSmearFrame(SkinnedMeshRenderer[] smrs, SmearFrameData smear,
            MotionData motion, int frame, Camera cam, RenderTexture rt, int res,
            bool captureUnlit, List<Material> tempMats)
        {
            var tempObjects = new List<GameObject>(smrs.Length + 2);
            var extraObjects = new List<GameObject>(2);
            try
            {
                int vertOffset = 0;
                for (int m = 0; m < smrs.Length; m++)
                {
                    var mesh = new Mesh();
                    smrs[m].BakeMesh(mesh);
                    var verts = mesh.vertices;

                    var smrT = smrs[m].transform;
                    for (int v = 0; v < verts.Length; v++)
                    {
                        int gi = vertOffset + v;
                        Vector3 baseWorld = motion.Vertices[frame][gi].position;
                        Vector3 deformedWorld = smear.DeformedPositions[frame][gi];
                        Vector3 delta = deformedWorld - baseWorld;

                        if (delta.sqrMagnitude > 0.000001f)
                            verts[v] += smrT.InverseTransformVector(delta);
                    }

                    mesh.vertices = verts;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    tempObjects.Add(CreateTempRenderer(smrs[m], mesh, captureUnlit, tempMats));
                    vertOffset += verts.Length;
                }


                if (smear.AdditionalGeometry != null && smear.AdditionalGeometry[frame] != null)
                {
                    var ghostGo = CreateGhostRenderer(smear.AdditionalGeometry[frame], smrs, captureUnlit, tempMats);
                    if (ghostGo != null)
                    {
                        ghostGo.layer = 0;
                        tempObjects.Add(ghostGo);
                        extraObjects.Add(ghostGo);
                    }
                }

                if (smear.MotionLineGeometry != null && smear.MotionLineGeometry[frame] != null)
                {
                    var lineGo = CreateLineRenderer(smear.MotionLineGeometry[frame], tempMats);
                    if (lineGo != null)
                    {
                        lineGo.layer = 0;
                        tempObjects.Add(lineGo);
                        extraObjects.Add(lineGo);
                    }
                }

                Texture2D bodyTex = null;
                if (extraObjects.Count > 0)
                {
                    cam.Render();
                    bodyTex = Readback(rt, res);
                    foreach (var go in extraObjects)
                        go.layer = CAPTURE_LAYER;
                }

                cam.Render();
                var mainTex = Readback(rt, res);

                Texture2D maskTex = null;
                if (extraObjects.Count > 0)
                {
                    foreach (var go in tempObjects)
                        if (!extraObjects.Contains(go)) go.layer = 0;
                    cam.Render();
                    var overlayTex = Readback(rt, res);
                    foreach (var go in tempObjects)
                        if (!extraObjects.Contains(go)) go.layer = CAPTURE_LAYER;

                    maskTex = SubtractOpaqueMask(overlayTex, bodyTex);
                    Object.DestroyImmediate(overlayTex);
                    if (bodyTex != null) Object.DestroyImmediate(bodyTex);
                }

                return (mainTex, maskTex);
            }
            finally
            {
                foreach (var go in tempObjects)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }

        // build a renderer for ghost copy geometry using the character's real texture + vertex alpha
        GameObject CreateGhostRenderer(Mesh addMesh, SkinnedMeshRenderer[] smrs, bool captureUnlit, List<Material> tempMats)
        {
            if (addMesh == null || smrs.Length == 0) return null;

            var go = new GameObject("_capGhost");
            go.layer = CAPTURE_LAYER;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = addMesh;

            var mr = go.AddComponent<MeshRenderer>();
            var ghostMat = BuildGhostMaterial(smrs, captureUnlit, tempMats);
            mr.sharedMaterial = ghostMat;
            return go;
        }

        // build a single ghost material: Ghost shader + the character's own color/texture so the copy stays visually aligned with the source
        private static Material BuildGhostMaterial(SkinnedMeshRenderer[] smrs, bool captureUnlit, List<Material> tempMats)
        {
            var shader = Shader.Find("SmearFramework/Ghost");
            if (shader == null)
                return null;

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_BaseColor", Color.white);
            tempMats.Add(mat);

            for (int s = 0; s < smrs.Length; s++)
            {
                foreach (var src in smrs[s].sharedMaterials)
                {
                    if (src == null) continue;

                    if (src.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                    else if (src.HasProperty("_Color")) mat.SetColor("_BaseColor", src.GetColor("_Color"));

                    Texture tex = null;
                    if (src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                    else if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                    if (tex != null)
                    {
                        mat.SetTexture("_MainTex", tex);
                        return mat;
                    }

                    return mat;
                }
            }

            return mat;
        }

        // build a renderer for motion-line geometry using its vertex colors directly
        GameObject CreateLineRenderer(Mesh addMesh, List<Material> tempMats)
        {
            if (addMesh == null) return null;

            var shader = Shader.Find("SmearFramework/Line");
            if (shader == null) return null;

            var go = new GameObject("_capLines");
            go.layer = CAPTURE_LAYER;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = addMesh;

            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            tempMats.Add(mat);
            mr.sharedMaterial = mat;
            return go;
        }

        // temp MeshRenderer on the capture layer; matches the original's world transform
        GameObject CreateTempRenderer(SkinnedMeshRenderer smr, Mesh mesh, bool captureUnlit, List<Material> tempMats)
        {
            var go = new GameObject("_cap");
            go.layer = CAPTURE_LAYER;

            // not parented - just copy world transform
            go.transform.position = smr.transform.position;
            go.transform.rotation = smr.transform.rotation;
            go.transform.localScale = smr.transform.lossyScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            if (captureUnlit)
                mr.materials = BuildUnlitMaterials(smr, tempMats); // flat albedo; no baked lighting
            else
                mr.sharedMaterials = smr.sharedMaterials;

            return go;
        }

        // build one UnlitCapture material per source submaterial, copying albedo texture + base color
        private static Material[] BuildUnlitMaterials(SkinnedMeshRenderer smr, List<Material> track)
        {
            var unlitShader = Shader.Find("SmearFramework/UnlitCapture");
            if (unlitShader == null) return smr.sharedMaterials; // shader not imported yet

            var result = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < smr.sharedMaterials.Length; i++)
            {
                var src = smr.sharedMaterials[i];
                var m = new Material(unlitShader) { hideFlags = HideFlags.HideAndDontSave };

                Texture tex = null;
                if (src != null)
                {
                    if (src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                    else if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                }
                if (tex != null) m.SetTexture("_MainTex", tex);

                Color col = Color.white;
                if (src != null)
                {
                    if (src.HasProperty("_BaseColor")) col = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) col = src.GetColor("_Color");
                }
                m.SetColor("_BaseColor", col);
                m.SetColor("_Color", Color.white);

                track.Add(m);
                result[i] = m;
            }
            return result;
        }

        // tight world-space bounds across all frames and all meshes
        Bounds ComputeBounds(GameObject target, SkinnedMeshRenderer[] smrs,
            ClipPoseSampler poseSampler, int frameCount, float dt)
        {
            // smr.bounds is stale in edit mode after clip sampling, so compute manually
            Bounds b = new Bounds(target.transform.position, Vector3.zero);
            bool init = false;
            var tempMesh = new Mesh();

            for (int f = 0; f < frameCount; f++)
            {
                poseSampler.Sample(f * dt);
                foreach (var smr in smrs)
                {
                    smr.BakeMesh(tempMesh);
                    (b, init) = EncapsulateVertices(b, init, tempMesh.vertices, smr.transform);
                }
            }

            Object.DestroyImmediate(tempMesh);
            return b;
        }

        // character-locked bounds from the current target pose; shared across clips that reuse the same source character
        Bounds ComputeReferenceBounds(GameObject target, SkinnedMeshRenderer[] smrs)
        {
            Bounds b = new Bounds(target.transform.position, Vector3.zero);
            bool init = false;
            var tempMesh = new Mesh();

            foreach (var smr in smrs)
            {
                smr.BakeMesh(tempMesh);
                (b, init) = EncapsulateVertices(b, init, tempMesh.vertices, smr.transform);
            }

            Object.DestroyImmediate(tempMesh);
            return b;
        }

        // grow bounds to include all verts from one baked mesh
        (Bounds, bool) EncapsulateVertices(Bounds b, bool init, Vector3[] verts, Transform smrT)
        {
            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 world = smrT.TransformPoint(verts[v]);
                if (!init) { b = new Bounds(world, Vector3.zero); init = true; }
                else b.Encapsulate(world);
            }
            return (b, init);
        }

        // subtract the body silhouette from the overlay mask so reserved smear color only applies outside the base character.
        private static Texture2D SubtractOpaqueMask(Texture2D overlayTex, Texture2D bodyTex)
        {
            if (overlayTex == null) return null;
            if (bodyTex == null) return overlayTex;

            var overlay = overlayTex.GetPixels32();
            var body = bodyTex.GetPixels32();
            for (int i = 0; i < overlay.Length && i < body.Length; i++)
            {
                if (body[i].a > 12)
                    overlay[i] = new Color32(0, 0, 0, 0);
            }

            var result = new Texture2D(overlayTex.width, overlayTex.height, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.SetPixels32(overlay);
            result.Apply();
            return result;
        }

        // GPU -> CPU readback
        Texture2D Readback(RenderTexture rt, int res)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }
    }
}
