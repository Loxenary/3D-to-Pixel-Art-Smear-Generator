using UnityEngine;

namespace SmearFramework.Editor
{
    [System.Serializable]
    internal struct PreviewViewState
    {
        public Vector2 Offset;
        public float Zoom;
        public float RollDeg;

        public static PreviewViewState Default => new PreviewViewState { Offset = Vector2.zero, Zoom = 1f, RollDeg = 0f };

        // Resets pan, zoom, and roll to the default preview transform.
        public void Reset()
        {
            Offset = Vector2.zero;
            Zoom = 1f;
            RollDeg = 0f;
        }
    }
}
