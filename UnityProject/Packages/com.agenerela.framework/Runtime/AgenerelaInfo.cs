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
        /// Package version. Kept in sync with <c>package.json</c> by hand for now;
        /// the smoke test asserts it is non-empty, not that it matches, because
        /// reading the manifest at runtime would pull in the Package Manager API.
        /// </summary>
        public const string Version = "0.1.0";
    }
}
