using System;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Generic 2D buffer indexed frame-first for sequential frame access (rendering, export). Element can be bone, vertex, etc. depending on usage.
    /// </summary>
    [Serializable]
    public class FrameBuffer<T>
    {
        T[][] _data;

        public int FrameCount => _data != null ? _data.Length : 0;

        public FrameBuffer(int frames, int elementsPerFrame)
        {
            _data = new T[frames][];
            for (int i = 0; i < frames; i++)
                _data[i] = new T[elementsPerFrame];
        }

        public T[] this[int frame]
        {
            get => _data != null ? _data[frame] : null;
            set { if (_data != null) _data[frame] = value; }
        }
    }

    /// <summary>
    /// Same as FrameBuffer but indexed element-first for walking one element's history across all frames contiguously in memory. Needed for spline fitting.
    /// </summary>
    [Serializable]
    public class ElementBuffer<T>
    {
        T[][] _data;

        public int ElementCount => _data != null ? _data.Length : 0;

        public ElementBuffer(int elements, int frames)
        {
            _data = new T[elements][];
            for (int i = 0; i < elements; i++)
                _data[i] = new T[frames];
        }

        public T[] this[int element]
        {
            get => _data != null ? _data[element] : null;
            set { if (_data != null) _data[element] = value; }
        }
    }
}
