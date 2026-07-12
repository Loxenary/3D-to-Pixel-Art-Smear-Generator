using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.VelocityExtraction
{
    // note: tremolieres-2025-trajectory-aware-smears.md
    // Builds per-vertex Catmull-Rom spline trajectories from sampled frame positions. Used to interpolate vertex motion at sub-frame resolution for smear generation.
    public class TrajectoryBuilder
    {
        // Transpose frame-major vertex positions into vertex-major spline control points
        public TrajectoryData Build(MotionData data)
        {
            var traj = new TrajectoryData(data.VertexCount, data.FrameCount);

            // transpose from frame-major (MotionData) to vertex-major (TrajectoryData)
            // so spline eval can iterate frames for a single vertex contiguously
            for (int v = 0; v < data.VertexCount; v++)
            {
                for (int f = 0; f < data.FrameCount; f++)
                    traj.ControlPoints[v][f] = data.Vertices[f][v].position;
            }

            return traj;
        }

        // Catmull-Rom spline evaluation, framePos can be fractional (e.g. 2.5 means halfway between frame 2 and 3)
        public Vector3 Evaluate(TrajectoryData traj, int vertexIndex, float framePos)
        {
            int maxFrame = traj.FrameCount - 1;
            framePos = Mathf.Clamp(framePos, 0f, maxFrame);

            int f1 = Mathf.FloorToInt(framePos);
            float t = framePos - f1;

            if (f1 >= maxFrame)
            {
                f1 = maxFrame;
                t = 0f;
            }

            // 4 control points with edge clamping
            int f0 = Mathf.Max(f1 - 1, 0);
            int f2 = Mathf.Min(f1 + 1, maxFrame);
            int f3 = Mathf.Min(f1 + 2, maxFrame);

            Vector3 p0 = traj.ControlPoints[vertexIndex][f0];
            Vector3 p1 = traj.ControlPoints[vertexIndex][f1];
            Vector3 p2 = traj.ControlPoints[vertexIndex][f2];
            Vector3 p3 = traj.ControlPoints[vertexIndex][f3];

            return CatmullRom(p0, p1, p2, p3, t);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            // standard Catmull-Rom matrix form
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}
