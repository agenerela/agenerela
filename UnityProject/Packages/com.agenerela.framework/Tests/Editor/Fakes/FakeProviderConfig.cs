using Agenerela.Providers;

namespace Agenerela.Tests
{
    /// <summary>
    /// A provider config whose provider is a <see cref="FakeProvider"/>, counting how often it was
    /// asked to build one, so a test can tell a cached provider from a rebuilt one (#92).
    /// </summary>
    public sealed class FakeProviderConfig : ProviderConfig
    {
        /// <summary>The reply each built provider answers with.</summary>
        public string Reply = "";

        /// <summary>When true, <see cref="CreateProvider"/> returns null, as a broken config would.</summary>
        public bool BuildsNothing;

        /// <summary>How many providers it has built.</summary>
        public int Builds { get; private set; }

        protected override ILLMProvider CreateProvider()
        {
            Builds++;
            return BuildsNothing ? null : new FakeProvider(Reply);
        }
    }
}
