using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.VelocityExtraction
{
    // note: basset-2024-smear-stylized-motion.md
    public class MotionOffsetComputer
    {
        const int MAX_INFLUENCES = 4; // unity skinning cap

        // scratch arrays, allocated once and reused per frame
        float[,] _rawDelta;
        float[,] _paramU;
        int[,] _influenceBone;
        float[] _maxRoot;
        float[] _maxTip;

        // runs the full offset computation: first normalize per frame, then smooth over time
        public void Compute(MotionData data, PipelineConfig config)
        {
            ComputeNormalizedOffsets(data);
            SmoothOverTime(data, config.TemporalSmoothingWindow);
        }

        // cross-joint normalization: two-pass per frame so offsets don't jump at joint boundaries (would happen with a single global max)
        void ComputeNormalizedOffsets(MotionData data)
        {
            int verts = data.VertexCount;
            int bones = data.BoneCount;

            AllocateScratchBuffers(verts, bones);

            for (int frame = 0; frame < data.FrameCount; frame++)
            {
                ResetPerFrameScratch(verts, bones);

                // pass 1: raw ribbon offset per (vertex, bone influence), also tracks the max offset in each bone's root vs tip region
                for (int v = 0; v < verts; v++)
                    ComputeVertexInfluences(data, frame, v);

                // pass 2: normalize by cross-joint factor, blend with skin weights
                for (int v = 0; v < verts; v++)
                    NormalizeVertex(data, frame, v);
            }
        }

        void AllocateScratchBuffers(int verts, int bones)
        {
            _rawDelta = new float[verts, MAX_INFLUENCES];
            _paramU = new float[verts, MAX_INFLUENCES]; // 0=bone root, 1=bone tip
            _influenceBone = new int[verts, MAX_INFLUENCES];
            _maxRoot = new float[bones];
            _maxTip = new float[bones];
        }

        void ResetPerFrameScratch(int verts, int bones)
        {
            System.Array.Clear(_maxRoot, 0, bones);
            System.Array.Clear(_maxTip, 0, bones);
            for (int v = 0; v < verts; v++)
                for (int k = 0; k < MAX_INFLUENCES; k++)
                    _influenceBone[v, k] = -1;
        }

        // for each skin influence on this vertex, compute the ribbon offset and track the per-bone max
        void ComputeVertexInfluences(MotionData data, int frame, int v)
        {
            var bw = data.SkinWeights[v];
            int[] idx = { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
            float[] wt = { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };
            Vector3 pos = data.Vertices[frame][v].position;

            for (int k = 0; k < MAX_INFLUENCES; k++)
            {
                if (wt[k] < 0.001f) continue;

                int bi = idx[k];
                float u;
                float delta = RibbonOffset(data, frame, bi, pos, data.Bones[frame][bi], out u);

                _rawDelta[v, k] = delta;
                _paramU[v, k] = u;
                _influenceBone[v, k] = bi;

                TrackRegionMax(bi, u, Mathf.Abs(delta));
            }
        }

        // keeps track of the largest offset in the root vs tip region of each bone, used for normalization
        void TrackRegionMax(int boneIndex, float u, float absDelta)
        {
            if (u < 0.5f)
                { if (absDelta > _maxRoot[boneIndex]) _maxRoot[boneIndex] = absDelta; }
            else
                { if (absDelta > _maxTip[boneIndex]) _maxTip[boneIndex] = absDelta; }
        }

        // blends all bone influences into one final offset per vertex, normalized by the cross-joint factor
        void NormalizeVertex(MotionData data, int frame, int v)
        {
            var bw = data.SkinWeights[v];
            float[] wt = { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };

            float blended = 0f;
            float totalW = 0f;

            for (int k = 0; k < MAX_INFLUENCES; k++)
            {
                int bi = _influenceBone[v, k];
                if (bi < 0) continue;

                // smooth blend between root-region max and tip-region max
                float u = _paramU[v, k];
                float M = (1f-u) * _maxRoot[bi] + u * _maxTip[bi];
                if (M < 0.0001f) M = Mathf.Max(_maxRoot[bi], _maxTip[bi]);

                float norm = (M > 0.0001f)
                    ? Mathf.Clamp(_rawDelta[v, k] / M, -1f, 1f)
                    : 0f;

                // propagated weights account for bone hierarchy
                float w = (data.PropagatedWeights != null)
                    ? data.PropagatedWeights[v][bi]
                    : wt[k];
                if (w < 0.001f) continue;

                blended += w * norm;
                totalW += w;
            }

            var snap = data.Vertices[frame][v];
            snap.motionOffset = totalW > 0f
                ? Mathf.Clamp(blended / totalW, -1f, 1f)
                : 0f;
            data.Vertices[frame][v] = snap;
        }

        // signed distance from vertex to the bone's motion-perpendicular "ribbon" plane. positive = vertex leads the motion, negative = trails behind. u (out): where the vertex sits along the bone, 0=root 1=tip.
        float RibbonOffset(MotionData data, int frame, int boneIdx,
            Vector3 vertPos, BoneSnapshot bone, out float u)
        {
            Vector3 axis = bone.rotation * Vector3.up;
            Vector3 toVert = vertPos - bone.position;

            u = ComputeBoneParameter(toVert, axis, GetBoneLength(data, frame, boneIdx));

            Vector3 dir = InterpolateMotionDirection(data, frame, boneIdx, bone, u);
            if (dir.sqrMagnitude < 0.0001f) { return 0f; }
            dir.Normalize();

            return ProjectOntoRibbon(toVert, dir, axis);
        }

        // figures out where along the bone this vertex sits (0 = root, 1 = tip)
        float ComputeBoneParameter(Vector3 toVert, Vector3 boneAxis, float boneLength)
        {
            if (boneLength < 0.0001f) return 0.5f;
            float proj = Vector3.Dot(toVert, boneAxis) / boneLength;
            return Smoothstep(Mathf.Clamp01(proj));
        }

        // slerps velocity direction from root bone to child bone based on where the vertex sits
        Vector3 InterpolateMotionDirection(MotionData data, int frame, int boneIdx, BoneSnapshot bone, float u)
        {
            Vector3 rootDir = bone.linearVelocity.normalized;
            Vector3 tipDir = rootDir;
            int child = FindChildBone(data, boneIdx);
            if (child >= 0)
                tipDir = data.Bones[frame][child].linearVelocity.normalized;

            // fallbacks if one end is stationary
            if (rootDir.sqrMagnitude < 0.001f) rootDir = tipDir;
            if (tipDir.sqrMagnitude < 0.001f) tipDir = rootDir;
            if (rootDir.sqrMagnitude < 0.001f) return Vector3.zero;

            if (Vector3.Dot(rootDir, tipDir) < -0.99f)
                return rootDir; // nearly opposite, slerp would flip

            return Vector3.Slerp(rootDir, tipDir, u);
        }

        // projects the vertex offset onto the ribbon normal and suppresses collinear motion
        float ProjectOntoRibbon(Vector3 toVert, Vector3 motionDir, Vector3 boneAxis)
        {
            // ribbon normal: perpendicular to bone axis, aligned with motion
            Vector3 normal = motionDir - Vector3.Dot(motionDir, boneAxis) * boneAxis;
            float nLen = normal.magnitude;
            if (nLen < 0.0001f) return 0f;
            normal /= nLen;

            float offset = Vector3.Dot(toVert, normal);

            // suppress when bone moves along its own axis (collinear = no useful ribbon)
            float coll = Vector3.Dot(motionDir, boneAxis);
            offset *= 1f - coll * coll;

            return offset;
        }

        float GetBoneLength(MotionData data, int frame, int boneIdx)
        {
            int child = FindChildBone(data, boneIdx);
            if (child < 0) return 0.1f; // leaf default
            return Vector3.Distance(data.Bones[frame][boneIdx].position, data.Bones[frame][child].position);
        }

        // TODO: cache - O(n) scan per call
        int FindChildBone(MotionData data, int boneIdx)
        {
            for (int b = 0; b < data.BoneCount; b++)
                if (data.ParentBoneIndex[b] == boneIdx) return b;
            return -1;
        }

        // temporal smoothing with a quartic kernel to reduce frame-to-frame noise
        void SmoothOverTime(MotionData data, int windowSize)
        {
            if (windowSize < 1) return;

            float[] kernel = BuildKernel(windowSize);
            var buf = new float[data.FrameCount];

            // convolve each vertex's offset timeline independently
            for (int v = 0; v < data.VertexCount; v++)
                SmoothVertex(data, v, kernel, windowSize, buf);
        }

        // reads the vertex's offset timeline into a buffer, convolves it, and writes back
        void SmoothVertex(MotionData data, int v, float[] kernel, int windowSize, float[] buf)
        {
            for (int f = 0; f < data.FrameCount; f++)
                buf[f] = data.Vertices[f][v].motionOffset;

            for (int f = 0; f < data.FrameCount; f++)
            {
                float smoothed = ConvolveAtFrame(buf, f, kernel, windowSize, data.FrameCount);
                var snap = data.Vertices[f][v];
                snap.motionOffset = smoothed;
                data.Vertices[f][v] = snap;
            }
        }

        float ConvolveAtFrame(float[] buf, int frame, float[] kernel, int windowSize, int frameCount)
        {
            float sum = 0f;
            for (int n = -windowSize; n <= windowSize; n++)
            {
                int idx = Mathf.Clamp(frame + n, 0, frameCount - 1);
                sum += kernel[n + windowSize] * buf[idx];
            }
            return sum;
        }

        // quartic kernel: (1-t^2)^2, wider in the middle, tapers at edges
        float[] BuildKernel(int halfWidth)
        {
            int size = 2 * halfWidth + 1;
            var k = new float[size];
            float total = 0f;

            for (int n = -halfWidth; n <= halfWidth; n++)
            {
                float t = (float)n / (halfWidth + 1);
                float w = 1f - t*t;
                k[n + halfWidth] = w * w;
                total += k[n + halfWidth];
            }
            for (int i = 0; i < size; i++)
                k[i] /= total;

            return k;
        }

        static float Smoothstep(float t) => t * t * (3f - 2f * t);
    }
}
