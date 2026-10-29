namespace Agenerela
{
    /// <summary>
    /// Package identity, surfaced for diagnostics and for stamping decision telemetry
    /// so a captured benchmark run can be traced back to the framework version that
    /// produced it.
    /// </summary>
    public static class AgenerelaInfo
    {
        /// <summary>Technical package name, matching <c>package.json</c>.</summary>
        public const string PackageName = "com.agenerela.framework";

        /// <summary>
        /// Package version, matching <c>package.json</c>. The smoke test asserts it is
        /// non-empty, not that it matches, because reading the manifest at runtime would
        /// pull in the Package Manager API; the hygiene check <c>version</c> compares the
        /// two instead, and <c>tools/ci/release.py prepare</c> changes both at once.
        /// </summary>
        public const string Version = "0.4.0-preview.1";
    }
}
