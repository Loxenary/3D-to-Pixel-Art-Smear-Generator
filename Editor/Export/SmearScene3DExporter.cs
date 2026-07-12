using System.IO;
using UnityEditor;
using UnityEngine;
using SmearFramework.AnimationSampling;
using SmearFramework.DataTypes;

namespace SmearFramework.Editor
{
    // Bakes the smear-augmented animation into a Unity-droppable 3D prefab.
    // The prefab uses Smear3DFramePlayer to cycle meshes -- no legacy Animation component.
    public static class SmearScene3DExporter
    {
        public class Result
        {
            public string PrefabPath;
            public string MeshFolder;
            public int FrameCount;
            public string ClipPath;
        }

        // Returns null when the inputs are not exportable (no character, no SMR, etc).
        public static Result Export(
            GameObject characterPrefab,
            AnimationClip sourceClip,
            SmearFrameData smear,
            MotionData motion,
            string outputDir,
            string baseName)
        {
            if (characterPrefab == null || sourceClip == null || smear == null || motion == null)
                return null;

            int frameCount = smear.FrameCount;
            if (frameCount <= 0) return null;

            float fps = motion.Fps > 0f ? motion.Fps : 12f;

            ClearExistingOutput(outputDir, baseName);
            AssetFolderUtility.EnsureAssetFolder(outputDir);
            string meshFolder = $"{outputDir}/{baseName}_smear3D_meshes";
            AssetFolderUtility.EnsureAssetFolder(meshFolder);

            // bake the per-frame meshes against a temp instance held at origin so the
            // resulting vertex coords are in the prefab root's local space
            var instance = Object.Instantiate(characterPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0)
            {
                Object.DestroyImmediate(instance);
                Debug.LogWarning("[SmearScene3DExporter] character has no SkinnedMeshRenderer, skipping 3D export");
                return null;
            }

            Mesh[] frameMeshes;
            Material sourceMaterial = smrs[0].sharedMaterial;
            try
            {
                frameMeshes = BakeFrameMeshes(instance, smrs, sourceClip, smear, motion, fps, meshFolder);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            var ghostMeshes = SaveGhostMeshes(smear, meshFolder);
            var lineMeshes  = SaveLineMeshes(smear, meshFolder);
            var ghostMat    = BuildGhostMaterial(sourceMaterial, outputDir, baseName);
            var lineMat     = BuildLineMaterial(outputDir, baseName);

            string prefabPath = $"{outputDir}/{baseName}_smear3D.prefab";

            BuildPrefab(prefabPath, baseName, frameMeshes, sourceMaterial, fps,
                ghostMeshes, ghostMat, lineMeshes, lineMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new Result
            {
                PrefabPath = prefabPath,
                MeshFolder = meshFolder,
                FrameCount = frameCount
            };
        }

        // walks the clip frame-by-frame, bakes all SMRs, applies smear deformation, and saves each
        // frame as a .asset. Ghost copies and motion lines are excluded -- they need their own
        // shaders and don't composite correctly into a single-material prefab.
        static Mesh[] BakeFrameMeshes(
            GameObject instance, SkinnedMeshRenderer[] smrs, AnimationClip clip,
            SmearFrameData smear, MotionData motion, float fps, string meshFolder)
        {
            int frameCount = smear.FrameCount;
            var meshes = new Mesh[frameCount];

            // reusable bake targets; one per SMR to avoid per-frame allocation
            var partBufs = new Mesh[smrs.Length];
            for (int m = 0; m < smrs.Length; m++)
                partBufs[m] = new Mesh();

            using (var poseSampler = new ClipPoseSampler(instance, clip))
            {
                for (int f = 0; f < frameCount; f++)
                {
                    float time = f / fps;
                    poseSampler.Sample(time);

                    var deformed = smear.DeformedPositions[f];
                    var baseVerts = motion.Vertices[f];

                    // bake each SMR in local space, apply smear deformation, then fold into world-space
                    var pieces = new CombineInstance[smrs.Length];
                    int vertOffset = 0;
                    for (int m = 0; m < smrs.Length; m++)
                    {
                        smrs[m].BakeMesh(partBufs[m]);
                        var verts = partBufs[m].vertices;
                        var smrT = smrs[m].transform;

                        if (deformed != null && baseVerts != null)
                        {
                            for (int v = 0; v < verts.Length; v++)
                            {
                                int gi = vertOffset + v;
                                if (gi >= deformed.Length || gi >= baseVerts.Length) break;
                                Vector3 delta = deformed[gi] - baseVerts[gi].position;
                                if (delta.sqrMagnitude > 0.000001f)
                                    verts[v] += smrT.InverseTransformVector(delta);
                            }
                            partBufs[m].vertices = verts;
                            partBufs[m].RecalculateNormals();
                        }

                        pieces[m].mesh = partBufs[m];
                        pieces[m].transform = smrT.localToWorldMatrix;
                        vertOffset += verts.Length;
                    }

                    var frameMesh = new Mesh { name = $"frame_{f:D3}" };
                    frameMesh.CombineMeshes(pieces, mergeSubMeshes: true, useMatrices: true);

                    string path = $"{meshFolder}/frame_{f:D3}.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(frameMesh, path);
                    meshes[f] = frameMesh;
                }
            }

            for (int m = 0; m < smrs.Length; m++)
                Object.DestroyImmediate(partBufs[m]);

            return meshes;
        }


        // Construct root + optional Ghost and Lines children, wire Smear3DFramePlayer, save as prefab.
        static void BuildPrefab(
            string prefabPath, string baseName,
            Mesh[] frames, Material material, float fps,
            Mesh[] ghostFrames, Material ghostMat,
            Mesh[] lineFrames, Material lineMat)
        {
            var root = new GameObject(baseName + "_Smear3D");
            try
            {
                var mf = root.AddComponent<MeshFilter>();
                mf.sharedMesh = frames != null && frames.Length > 0 ? frames[0] : null;

                var mr = root.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;

                if (ghostMat != null && System.Array.Exists(ghostFrames, m => m != null))
                {
                    var ghostGo = new GameObject("Ghost");
                    ghostGo.transform.SetParent(root.transform, false);
                    ghostGo.AddComponent<MeshFilter>().sharedMesh = FirstNonNull(ghostFrames);
                    ghostGo.AddComponent<MeshRenderer>().sharedMaterial = ghostMat;
                }

                if (lineMat != null && System.Array.Exists(lineFrames, m => m != null))
                {
                    var lineGo = new GameObject("Lines");
                    lineGo.transform.SetParent(root.transform, false);
                    lineGo.AddComponent<MeshFilter>().sharedMesh = FirstNonNull(lineFrames);
                    lineGo.AddComponent<MeshRenderer>().sharedMaterial = lineMat;
                }

                var player = root.AddComponent<Smear3DFramePlayer>();
                player.Init(
                    frames,
                    System.Array.Exists(ghostFrames, m => m != null) ? ghostFrames : null,
                    System.Array.Exists(lineFrames,  m => m != null) ? lineFrames  : null,
                    fps);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }


        // Deletes any existing prefab, clip, mesh folder, and material assets for a previous run.
        public static void ClearExistingOutput(string outputDir, string baseName)
        {
            if (string.IsNullOrEmpty(outputDir) || string.IsNullOrEmpty(baseName)) return;
            string prefabPath = $"{outputDir}/{baseName}_smear3D.prefab";
            string clipPath   = $"{outputDir}/{baseName}_smear3D.anim";
            string meshFolder = $"{outputDir}/{baseName}_smear3D_meshes";
            string ghostMat   = $"{outputDir}/{baseName}_ghost_mat.mat";
            string lineMat    = $"{outputDir}/{baseName}_line_mat.mat";
            if (prefabPath.StartsWith("Assets")) AssetDatabase.DeleteAsset(prefabPath);
            if (clipPath.StartsWith("Assets"))   AssetDatabase.DeleteAsset(clipPath);
            if (meshFolder.StartsWith("Assets")) AssetDatabase.DeleteAsset(meshFolder);
            if (ghostMat.StartsWith("Assets"))   AssetDatabase.DeleteAsset(ghostMat);
            if (lineMat.StartsWith("Assets"))    AssetDatabase.DeleteAsset(lineMat);
        }

        // Clone and persist per-frame ghost copy meshes. Frames with no ghost return null.
        static Mesh[] SaveGhostMeshes(SmearFrameData smear, string meshFolder)
        {
            int frameCount = smear.FrameCount;
            var meshes = new Mesh[frameCount];
            if (smear.AdditionalGeometry == null) return meshes;

            string ghostFolder = $"{meshFolder}/ghost";
            AssetFolderUtility.EnsureAssetFolder(ghostFolder);

            for (int f = 0; f < frameCount; f++)
            {
                var src = smear.AdditionalGeometry[f];
                if (src == null || src.vertexCount == 0) continue;
                var clone = Object.Instantiate(src);
                clone.name = $"ghost_{f:D3}";
                string path = $"{ghostFolder}/ghost_{f:D3}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(clone, path);
                meshes[f] = clone;
            }
            return meshes;
        }

        // Clone and persist per-frame motion line meshes. Frames with no lines return null.
        static Mesh[] SaveLineMeshes(SmearFrameData smear, string meshFolder)
        {
            int frameCount = smear.FrameCount;
            var meshes = new Mesh[frameCount];
            if (smear.MotionLineGeometry == null) return meshes;

            string lineFolder = $"{meshFolder}/lines";
            AssetFolderUtility.EnsureAssetFolder(lineFolder);

            for (int f = 0; f < frameCount; f++)
            {
                var src = smear.MotionLineGeometry[f];
                if (src == null || src.vertexCount == 0) continue;
                var clone = Object.Instantiate(src);
                clone.name = $"line_{f:D3}";
                string path = $"{lineFolder}/line_{f:D3}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(clone, path);
                meshes[f] = clone;
            }
            return meshes;
        }

        // Build and save the ghost copy material from the source character's texture and tint.
        static Material BuildGhostMaterial(Material sourceMaterial, string outputDir, string baseName)
        {
            var shader = Shader.Find("SmearFramework/Ghost");
            if (shader == null)
            {
                Debug.LogWarning("[SmearScene3DExporter] SmearFramework/Ghost shader not found");
                shader = Shader.Find("Unlit/Transparent");
            }
            if (shader == null) return null;

            var mat = new Material(shader) { name = $"{baseName}_ghost" };
            if (sourceMaterial != null)
            {
                Texture tex = null;
                if (sourceMaterial.HasProperty("_BaseMap"))  tex = sourceMaterial.GetTexture("_BaseMap");
                if (tex == null && sourceMaterial.HasProperty("_MainTex")) tex = sourceMaterial.GetTexture("_MainTex");
                if (tex != null) mat.SetTexture("_MainTex", tex);

                Color col = Color.white;
                if (sourceMaterial.HasProperty("_BaseColor")) col = sourceMaterial.GetColor("_BaseColor");
                else if (sourceMaterial.HasProperty("_Color")) col = sourceMaterial.GetColor("_Color");
                mat.SetColor("_BaseColor", col);
            }

            string path = $"{outputDir}/{baseName}_ghost_mat.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Build and save the motion line material (vertex-color only, no texture needed).
        static Material BuildLineMaterial(string outputDir, string baseName)
        {
            var shader = Shader.Find("SmearFramework/Line");
            if (shader == null)
            {
                Debug.LogWarning("[SmearScene3DExporter] SmearFramework/Line shader not found");
                return null;
            }
            var mat = new Material(shader) { name = $"{baseName}_line" };
            string path = $"{outputDir}/{baseName}_line_mat.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Set object-reference keyframes for MeshFilter.m_Mesh on childPath across all frames.
        // null entries produce a null keyframe -- Unity clears the MeshFilter on that frame.
        static void SetMeshCurve(AnimationClip clip, string childPath, Mesh[] frames, float fps)
        {
            var keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int f = 0; f < frames.Length; f++)
                keyframes[f] = new ObjectReferenceKeyframe { time = f / fps, value = frames[f] };
            var binding = EditorCurveBinding.PPtrCurve(childPath, typeof(MeshFilter), "m_Mesh");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        }

        // Returns the first non-null mesh in the array for use as the prefab's initial display mesh.
        static Mesh FirstNonNull(Mesh[] meshes)
        {
            if (meshes == null) return null;
            foreach (var m in meshes)
                if (m != null) return m;
            return null;
        }
    }
}
