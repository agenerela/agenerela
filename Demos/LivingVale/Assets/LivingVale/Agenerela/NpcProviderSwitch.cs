using System;
using Agenerela.Providers;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// F6: whether the NPCs answer from the script or from the model. Both are providers of the same
    /// agents (<see cref="AgenerelaNpcDecisionDriver.UseProvider"/>, #198), so switching keeps every
    /// NPC's memory, and a reply already decided is carried out by the same agent whichever is
    /// selected by then. F7 changes the model provider here, whether or not it is selected.
    /// </summary>
    public sealed class NpcProviderSwitch
    {
        private readonly ScriptedReplies scripted;

        /// <param name="driver">The NPCs' driver; its provider at the start is the model's.</param>
        public NpcProviderSwitch(AgenerelaNpcDecisionDriver driver, ScriptedReplies scripted, NpcDriverChoice initialSelection)
        {
            Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.scripted = scripted ?? throw new ArgumentNullException(nameof(scripted));
            ModelProvider = driver.Provider;
            ModelLabel = driver.ModelLabel;
            Select(initialSelection);
        }

        public AgenerelaNpcDecisionDriver Driver { get; }

        public NpcDriverChoice Choice { get; private set; }

        /// <summary>The model's provider, used while <see cref="NpcDriverChoice.Agenerela"/> is selected.</summary>
        public ILLMProvider ModelProvider { get; private set; }

        /// <summary>The model's name, such as <c>gemma4:e2b-it-qat</c>.</summary>
        public string ModelLabel { get; private set; }

        public string Label => Driver.Label;

        public void Select(NpcDriverChoice choice)
        {
            if (choice != NpcDriverChoice.Scripted && choice != NpcDriverChoice.Agenerela)
            {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }

            Choice = choice;
            Apply();
        }

        public void Next() => Select(Choice == NpcDriverChoice.Scripted ? NpcDriverChoice.Agenerela : NpcDriverChoice.Scripted);

        /// <summary>Another model, for F7. The NPCs use it at once when the model is selected, otherwise from the next F6.</summary>
        public void UseModel(ILLMProvider provider, string modelLabel)
        {
            ModelProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            ModelLabel = modelLabel;
            Apply();
        }

        private void Apply()
        {
            if (Choice == NpcDriverChoice.Scripted)
            {
                Driver.UseProvider(scripted.Provider, ScriptedProvider.ProviderName, () => scripted.Label);
            }
            else
            {
                Driver.UseProvider(ModelProvider, ModelLabel);
            }
        }
    }
}
