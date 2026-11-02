using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    /// <summary>
    /// What only Play mode can show about <see cref="AgentBehaviour"/> (#93): Unity calling
    /// <c>Awake</c>, and a real destruction raising <c>destroyCancellationToken</c>, which
    /// <c>DestroyImmediate</c> in EditMode does not. No model is involved, so these are not in the
    /// <c>RequiresOllama</c> category. <c>AgentBehaviourTests</c> has the rest.
    /// </summary>
    public sealed class AgentBehaviourPlayModeTests
    {
        private HoldingConfig config;
        private AgentProfile profile;
        private GameObject guardObject;

        /// <summary>A provider whose reply never comes: it only ends by being cancelled.</summary>
        private sealed class HoldingProvider : ILLMProvider
        {
            public string Name => "holding";

            public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities();

            public CancellationToken Token { get; private set; }

            public int Requests { get; private set; }

            public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
            {
                Requests++;
                Token = ct;
                var source = new AwaitableCompletionSource<ProviderResult>();
                ct.Register(() => source.TrySetCanceled());
                return source.Awaitable;
            }
        }

        private sealed class HoldingConfig : ProviderConfig
        {
            public int Builds { get; private set; }

            protected override ILLMProvider CreateProvider()
            {
                Builds++;
                return new HoldingProvider();
            }
        }

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HoldingConfig>();
            profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.Identity = new AgentIdentity { Name = "Guard" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(guardObject);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator UnityCallingAwakeBuildsTheAgent()
        {
            var guard = InactiveGuard();
            Assert.That(config.Builds, Is.EqualTo(0));

            guardObject.SetActive(true);
            yield return null;

            Assert.That(config.Builds, Is.EqualTo(1));
            Assert.That(guard.Agent, Is.Not.Null);
            Assert.That(config.Builds, Is.EqualTo(1), "The read returned the agent Awake built.");
        }

        [UnityTest]
        public IEnumerator ADecisionInFlightWhenTheGameObjectIsDestroyedEndsCancelled()
        {
            var guard = InactiveGuard();
            guardObject.SetActive(true);
            yield return null;

            var provider = (HoldingProvider)config.Provider;
            var ended = EndsCancelled(guard.DecideAsync("Halt!"));

            Assert.That(provider.Requests, Is.EqualTo(1));
            Assert.That(provider.Token.IsCancellationRequested, Is.False);

            Object.Destroy(guardObject);
            yield return null;

            Assert.That(provider.Token.IsCancellationRequested, Is.True, "The provider saw the cancellation.");

            for (int frame = 0; frame < 10 && !ended.IsCompleted; frame++)
            {
                yield return null;
            }

            Assert.That(ended.IsCompleted, Is.True, "The decision ended.");
            Assert.That(ended.Result, Is.True, "A destroyed guard's decision has no result.");
        }

        // A limit of Unity's, pinned so a change in it shows up: it raises the token only for a
        // component that woke. AgentBehaviour.DecideAsync documents this.
        [UnityTest]
        public IEnumerator AGuardNeverActivatedIsNotCancelledByItsDestruction()
        {
            var guard = InactiveGuard();
            var provider = (HoldingProvider)config.Provider;
            _ = guard.DecideAsync("Halt!");

            Object.Destroy(guardObject);
            yield return null;

            Assert.That(guard == null, Is.True);
            Assert.That(provider.Token.IsCancellationRequested, Is.False);
        }

        private AgentBehaviour InactiveGuard()
        {
            guardObject = new GameObject("Guard");
            guardObject.SetActive(false);
            var guard = guardObject.AddComponent<AgentBehaviour>();
            guard.Profile = profile;
            guard.Provider = config;
            return guard;
        }

        private static async Task<bool> EndsCancelled(Awaitable<DecisionResult> decision)
        {
            try
            {
                await decision;
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }
    }
}
