namespace SmearFramework
{
    // Central asset paths for the framework's source, editor tools, and generated assets.
    public static class SmearFrameworkPaths
    {
        public const string Root = "Packages/com.davis.smear-generator";
        public const string GeneratedRoot = "Assets/SmearGenerator.Generated";
        public const string Output = GeneratedRoot + "/Output";
        public const string TempOutput = GeneratedRoot + "/Temp";
        public const string ImportedPackages = GeneratedRoot + "/ImportedPackages";
        public const string Smear3DTempOutput = TempOutput + "/Smear3D";
        public const string DiagnosticsOutput = GeneratedRoot + "/Diagnostics";
        public const string DefaultData = Root + "/Editor/DefaultData";
        public const string PackageTestData = Root + "/Tests/TestData";
        public const string TestTemp = TempOutput + "/Tests";

        // Combines two asset path segments without duplicate slashes.
        public static string Join(string a, string b) => a.TrimEnd('/') + "/" + b.TrimStart('/');

        // Combines three asset path segments without duplicate slashes.
        public static string Join(string a, string b, string c) => Join(Join(a, b), c);
    }
}
