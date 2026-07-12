using System;

namespace SmearFramework
{
    public readonly struct ArtifactKey : IEquatable<ArtifactKey>
    {
        public string Key { get; }
        public Type Type { get; }

        public ArtifactKey(string key, Type type)
        {
            Key = key;
            Type = type;
        }

        public static ArtifactKey Of<T>(string key) => new ArtifactKey(key, typeof(T));

        public bool Equals(ArtifactKey other) => Key == other.Key && Type == other.Type;

        public override bool Equals(object obj) => obj is ArtifactKey other && Equals(other);

        public override int GetHashCode() => (Key, Type).GetHashCode();
    }
}
