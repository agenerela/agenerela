using Agenerela.Providers;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// A provider config whose provider is the test's <see cref="FakeVillageProvider"/>. Every agent
    /// given it gets that one instance, so they share one queue, as on the real config.
    /// </summary>
    internal sealed class FakeVillageConfig : ProviderConfig
    {
        public FakeVillageProvider Fake;

        protected override ILLMProvider CreateProvider() => Fake;
    }
}
