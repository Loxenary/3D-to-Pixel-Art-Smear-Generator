using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    [Serializable]
    public class RawFrameData
    {
        private int _frameCount;
        private int _width;
        private int _height;
        private Texture2D[] _frames;
        private Texture2D[] _smearMasks; // per-frame mask of additional geometry (multiples/motion lines); null entries = no overlay that frame

        public int FrameCount => _frameCount;
        public int Width => _width;
        public int Height => _height;
        public Texture2D[] Frames => _frames;
        public Texture2D[] SmearMasks => _smearMasks;

        public RawFrameData(int frameCount, int width, int height)
        {
            _frameCount = frameCount;
            _width = width;
            _height = height;
            _frames = new Texture2D[frameCount];
            _smearMasks = new Texture2D[frameCount];
        }
    }
}
