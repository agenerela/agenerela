using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// The settings for one call to <see cref="Agent.DecideAsync"/>. Every field has a default, and
    /// a null <c>DecideOptions</c> means all of them.
    /// </summary>
    /// <remarks>
    /// Open on purpose (DR-008): free strings and switches the developer sets per call, never an
    /// enum of situations. Whether this call is a player's line, a timer or a report is the
    /// developer's knowledge, and the framework never asks. What comes later goes here as more
    /// switches of the same kind, such as Phase 3's per-call guard settings, starting with
    /// <see cref="CheckTarget"/> (DR-016). <see cref="Priority"/> is one too: it says whether anyone
    /// is waiting, never why the agent is asked (DR-015).
    /// </remarks>
    public sealed class DecideOptions
    {
        /// <summary>
        /// How the stimulus is labelled in the prompt, and in the examples: "Player", "Advisor",
        /// "Report", "Situation"... Any non-empty string; an empty one is a mistake the call
        /// throws for.
        /// </summary>
        public string StimulusLabel = "Player";

        /// <summary>
        /// The stimulus of the idle example, the one answered with <c>none</c>, for this call only.
        /// Null uses the agent's <see cref="Agent.IdleExampleStimulus"/>, and when that is null too,
        /// the profile's <see cref="AgentProfile.IdleExampleStimulus"/>; empty leaves the example out
        /// of this call.
        /// </summary>
        public string IdleExampleStimulus;

        /// <summary>
        /// What the agent says in the idle example, for this call only (#190, G14). Null uses the
        /// agent's <see cref="Agent.IdleExampleStatement"/>, then the profile's
        /// <see cref="AgentProfile.IdleExampleStatement"/>, but never from further down than the
        /// idle example's stimulus came from; empty gives the example no statement.
        /// </summary>
        /// <remarks>
        /// So a profile's statement is never attached to a line this call or the agent supplied: set
        /// <see cref="IdleExampleStimulus"/> here, and its statement comes from here or nowhere.
        /// </remarks>
        public string IdleExampleStatement;

        /// <summary>
        /// Whether this call's examples show the statements developers wrote: each action's
        /// <see cref="ActionDefinition.ExampleStatement"/> and <see cref="ActionDefinition.CounterExampleStatement"/>,
        /// and the idle example's (#190, G14). On by default, which shows only what was written: every
        /// statement is empty unless someone fills it. False leaves them all out, as an A/B run's
        /// control arm does.
        /// </summary>
        public bool ExampleStatements = true;

        /// <summary>
        /// Whether this call's examples show their situation: each action's
        /// <see cref="ActionDefinition.ExampleObservations"/>, rendered above its example as a real
        /// call's observations are (#216 item 1). Off by default: it changes the examples, and is owed
        /// an A/B run (#151). With it off, or with no example observations written, the block is
        /// byte-identical to one built before it existed.
        /// </summary>
        public bool ExampleObservations;

        /// <summary>
        /// Whether an example whose action has no registered target among its
        /// <see cref="ActionDefinition.PreferredExampleTargets"/> names the first offered target the
        /// action takes, rather than being left out (#190, Living Vale #20). Off by default: it changes
        /// the examples, and is owed an A/B run (#151).
        /// </summary>
        /// <remarks>
        /// For a game whose targets change every turn, so no fixed list stays right. "Takes" is
        /// <see cref="ActionDefinition.AcceptsCategory"/>: any target, unless the action names its
        /// categories. A target the stimulus mentions is skipped, by any word of its id or name said
        /// whole ("ox"), or starting a word of the stimulus from three letters ("inn" in innkeeper), or
        /// started by one of four or more ("dock" for docks), so the example never shows the answer;
        /// with every target the action takes
        /// mentioned, the example is left out. A target that implements <see cref="INamedTarget"/> is
        /// called by its name. A registered preferred target is used as before, mentioned or not.
        /// </remarks>
        public bool ExampleTargetFallback;

        /// <summary>
        /// Whether this call's system prompt lists the actions offered with their descriptions, and
        /// the targets offered with their own lines (#190). Off by default: unmeasured, and owed an
        /// A/B run before it becomes the default (#151). <see cref="PromptOptions.ListActionsAndTargets"/>
        /// says what it adds.
        /// </summary>
        /// <remarks>
        /// For a provider that never reads the schema, such as Ollama, where a model otherwise learns
        /// the actions only from the few-shot examples. A target object says what it is by
        /// implementing <see cref="IDescribedTarget"/>.
        /// </remarks>
        public bool ListActionsAndTargets;

        /// <summary>
        /// The most targets <see cref="ListActionsAndTargets"/> lists, 12 by default
        /// (<see cref="PromptOptions.MaxListedTargets"/>). A negative value is a mistake the call
        /// throws for.
        /// </summary>
        public int MaxListedTargets = PromptOptions.DefaultMaxListedTargets;

        /// <summary>
        /// Whether this call's system prompt says which targets each offered action may take now,
        /// and how (#151, <see cref="LegalTargetListing"/>). Off by default: unmeasured as a default,
        /// and with it off every request is byte-identical to one sent before it existed.
        /// </summary>
        /// <remarks>
        /// With <see cref="LegalTargetListing.PerTarget"/> the actions and targets are listed whether
        /// <see cref="ListActionsAndTargets"/> is on or not, with the pairs the paired schema would
        /// offer (<see cref="PairTargetsWithActions"/>); with <see cref="LegalTargetListing.ObservationLine"/>
        /// the same pairs are one sentence among the observations, and the system prompt is what
        /// <see cref="ListActionsAndTargets"/> makes it. Either way an action that requires a target
        /// and may take none is not offered, as with pairing. The
        /// schema stays flat unless pairing is on too: this tells the model the pairs, pairing makes
        /// the grammar keep to them, and the guards refuse a pair that breaks them either way.
        /// </remarks>
        public LegalTargetListing LegalTargets = LegalTargetListing.Off;

        /// <summary>
        /// How this call's system prompt introduces the identity's
        /// <see cref="AgentIdentity.StatementRules"/> (#244 item 8, <see cref="Agenerela.StatementRulesWording"/>).
        /// <see cref="Agenerela.StatementRulesWording.YourStatement"/> by default, the line every
        /// request carried before this option; with it every request is byte-identical to one sent
        /// before.
        /// </summary>
        /// <remarks>
        /// For an identity whose rules a small model copies out as its statement. Measured on 11
        /// November 2026 (findings, "How a statement rule is introduced"); the default stays until a
        /// decision record says otherwise. An identity without rules is not changed by any value.
        /// </remarks>
        public StatementRulesWording StatementRulesWording = StatementRulesWording.YourStatement;

        /// <summary>
        /// Whether this call asks for the free-text <c>statement</c> at all (#266). On by default,
        /// the request every call sent before this option, byte for byte. False leaves the field out
        /// of the schema, flat, without targets and paired alike, and out of what the prompt says
        /// about it, so the reply is <c>action</c> and <c>target</c> alone.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For a call whose move is all the game needs, such as a marshal ordering ten armies a turn:
        /// asked to speak on each, a model repeats itself (GreyBoxStrategy, findings 13 November
        /// 2026). The field order stays <c>action</c>, <c>target</c> (DR-004).
        /// </para>
        /// <para>
        /// Off, the examples show no statement, the identity's <see cref="AgentIdentity.StatementRules"/>
        /// line is not sent, and the target rule, when it is sent, no longer asks the agent to say
        /// that a thing is not here. The decision's <see cref="AgentDecision.Statement"/> is empty,
        /// even if the provider answered with one anyway; memory remembers the reply without it, as
        /// the schema asked. A decision a guard stops keeps its empty statement:
        /// <see cref="Agent.RefusalStatement"/> is not asked, and <see cref="AskForRefusalLine"/>
        /// sends nothing. <see cref="DecisionTelemetry.StatementAsked"/> records the choice. A guard
        /// of your own that judges the statement should pass a call that asked for none, by
        /// reading this option on <see cref="GuardInput.Options"/>.
        /// </para>
        /// <para>
        /// It changes what the model reads, so it is a choice per call, never a default, until an
        /// A/B run says otherwise (#151). Nothing here classifies the call (DR-008).
        /// </para>
        /// </remarks>
        public bool AskForStatement = true;

        /// <summary>
        /// Whether this call's examples show each action's
        /// <see cref="ActionDefinition.CounterExampleStimulus"/>, answered with <c>none</c> (#190). On
        /// by default, which shows only what a developer wrote. False leaves them out of this call, as
        /// the control arm of an A/B run does.
        /// </summary>
        public bool CounterExamples = true;

        /// <summary>
        /// Whether this call's schema pairs each action with the targets it takes, so the grammar
        /// cannot sample an action with a target of another kind (#190 B, DR-020). Off by default:
        /// unmeasured, and owed an A/B run before it becomes the default (#151).
        /// </summary>
        /// <remarks>
        /// <para>
        /// On, <see cref="DecisionSchema.Build(AgentContext, bool)"/> says what each action takes: its
        /// <see cref="ActionDefinition.TargetCategories"/>, or every target when it names none, and
        /// <c>no_target</c> only when it does not require a target; <c>none</c> takes
        /// <c>no_target</c> alone, as does an action with <see cref="ActionDefinition.TakesNoTarget"/>. An action that requires a target and takes none of those
        /// registered is not offered, and its example is not shown. The prompt, its action and target
        /// list and the target rule are what they would be without it.
        /// </para>
        /// <para>
        /// Through <see cref="JsonSchemaSerializer"/> it is a root <c>anyOf</c>, which llama.cpp, and
        /// so Ollama, compiles into one grammar. A provider that cannot take a root <c>anyOf</c>, such
        /// as OpenAI's strict structured outputs, should be sent the flat schema; none ships yet.
        /// </para>
        /// </remarks>
        public bool PairTargetsWithActions;

        /// <summary>
        /// What the game wants the agent to know right now, one fact per entry, sent verbatim. The
        /// list is copied when the call starts, so changing it afterwards changes nothing.
        /// </summary>
        /// <remarks>
        /// Observations are what the model reads about the world. <see cref="Agent.State"/> is
        /// not: it stays with the developer's handlers and never reaches the prompt.
        /// </remarks>
        public IList<string> Observations = new List<string>();

        /// <summary>
        /// Options to choose among on this call, instead of the agent's registered actions (DR-022,
        /// #266 item 2). Null by default: the call offers the agent's actions, as every call did before.
        /// </summary>
        /// <remarks>
        /// <para>
        /// With any choices, the reply's <c>action</c> is one of the available choices' ids, or
        /// <see cref="ActionRegistry.None"/>, offered last as on every call; the schema has no
        /// <c>target</c> field, since a choice takes none. The system prompt lists the offered choices
        /// with their descriptions whatever <see cref="ListActionsAndTargets"/> says, because a model
        /// served through Ollama never reads the schema. A choice that is not
        /// <see cref="Choice.Available"/> is left out of both, its reason in
        /// <see cref="DecisionTelemetry.ChoicesUnavailable"/>; <see cref="DecisionTelemetry.ChoicesOffered"/>
        /// lists the rest. The agent's identity, observations, memory and guards are those of any
        /// other call. Its registered actions and targets are not offered, and none of their
        /// examples is shown; the idle example is.
        /// </para>
        /// <para>
        /// The game acts on the chosen id, <see cref="AgentDecision.ActionId"/>. <see cref="Agent.Execute(DecisionResult)"/>
        /// runs no handler and no re-check for it, records <see cref="ExecutionStatus.Done"/> and
        /// settles memory held by <see cref="MemoryCommit.OnExecute"/>. A remembered choice is replayed
        /// to later calls like any decision.
        /// </para>
        /// <para>
        /// An empty list, a null choice, an id an action could not have (<see cref="ActionDefinition.ValidateId"/>),
        /// <c>none</c>, an id given twice or an empty description is a mistake the call throws for. The
        /// list is copied when the call starts. The prompt a choice call sends is new and unmeasured (#151).
        /// </para>
        /// </remarks>
        public IList<Choice> Choices;

        /// <summary>
        /// Where this call's own <see cref="Observations"/> go in the request (#151):
        /// <see cref="Agenerela.ObservationPlacement.AfterSources"/> by default, the end of the
        /// observation block, after every line of <see cref="Agent.ObservationSources"/>, which is
        /// what every request sent before this option.
        /// <see cref="Agenerela.ObservationPlacement.WithStimulus"/> puts them after the stimulus's
        /// closing quote in its user turn instead.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Choose <see cref="Agenerela.ObservationPlacement.WithStimulus"/> when this call's lines are
        /// the facts of this one moment (a wolf has cornered a child) and what holds on every call
        /// (the time of day, the agent's post) comes from an observation source: then it won an
        /// interleaved A/B run on both models it was measured on (findings, 10 November 2026). With
        /// every fact passed here and no source, it moves the whole block, which is measured
        /// separately.
        /// </para>
        /// <para>
        /// Only where the lines are rendered changes. They count against
        /// <see cref="Agent.MaxObservations"/> the same way, and the context every guard, source and
        /// handler reads holds the same list in the same order. Nothing here reads
        /// <see cref="StimulusLabel"/> or the stimulus (DR-008).
        /// </para>
        /// </remarks>
        public ObservationPlacement ObservationPlacement = ObservationPlacement.AfterSources;

        /// <summary>
        /// Whether anyone is waiting on this decision (DR-015). Every agent on one provider shares
        /// its queue, which sends a <see cref="DecisionPriority.PlayerFacing"/> request before any
        /// <see cref="DecisionPriority.Background"/> one. Player-facing by default; set it to
        /// background for ambient behaviour, a timer or anything else nobody is waiting for.
        /// </summary>
        /// <remarks>
        /// Going first means jumping the queue: a background request already sent is not aborted.
        /// The decision's telemetry records the priority, how long it waited and behind how many.
        /// </remarks>
        public DecisionPriority Priority = DecisionPriority.PlayerFacing;

        /// <summary>
        /// Whether <see cref="TargetGroundingGuard"/> checks this call's target: whether the model
        /// wanted something that is not on the list, and so would act on the nearest listed target
        /// instead (DR-016). On by default, whoever calls. Turn it off for a call where the agent
        /// should act on its best guess rather than do nothing.
        /// </summary>
        /// <remarks>
        /// Off, the guard does not run on that call, and the decision's telemetry records
        /// <see cref="GroundingStatus.Off"/>. It also turns off the guard's fallback: on a provider
        /// that reports no token probabilities, the prompt carries the one-line target rule only
        /// while this is on (<see cref="PromptOptions.TargetRule"/>, #107).
        /// </remarks>
        public bool CheckTarget = true;

        /// <summary>
        /// The share of the model's probability at its target that must stay on the target list for
        /// <see cref="TargetGroundingGuard"/> to pass this call's decision, from 0 to 1:
        /// <see cref="TargetGroundingGuard.DefaultThreshold"/>, 0.8, unless set (#244). A value
        /// outside 0 to 1 is a mistake the call throws for.
        /// </summary>
        /// <remarks>
        /// Lower it for a call where several targets are often right at once, such as a strategy
        /// game's provinces, rather than turning <see cref="CheckTarget"/> off. Know the trade
        /// (findings, 11 November 2026): at 0.6, GreyBoxStrategy's model attacked as often as with
        /// the guard off, while on the interim evaluation set <c>qwen3.5:2b</c> carried out two more
        /// of 64 impossible requests than at 0.8. So it suits a game with many legal targets a turn,
        /// not one where refusing the impossible matters most. 0 never stops a decision but still
        /// records the reading.
        /// </remarks>
        public float TargetGroundingThreshold = TargetGroundingGuard.DefaultThreshold;

        /// <summary>
        /// Whether to ask the model what to say when a guard stops this decision and
        /// <see cref="Agent.RefusalStatement"/> gave no line (#109). Off by default, so a stopped
        /// decision's statement is empty. Per call, because only the caller knows whether anyone is
        /// listening (DR-008).
        /// </summary>
        /// <remarks>
        /// It sends one more short request through the same provider and queue, at this call's
        /// <see cref="Priority"/>, and only when a guard fired. It never changes which action runs,
        /// and if it fails the decision stands with an empty statement. Its cost is recorded apart
        /// from the decision's, in <see cref="DecisionTelemetry.RefusalLineSeconds"/> and the fields
        /// beside it. The wording of that request is unmeasured.
        /// </remarks>
        public bool AskForRefusalLine;

        /// <summary>
        /// The sampling temperature for this call, or null, the default, for the provider's own
        /// (<see cref="Providers.OllamaSettings.Temperature"/>, 0) (#266 item 7). Sent by Ollama as
        /// <c>options.temperature</c>. A negative, NaN or infinite value is a mistake the call throws for.
        /// </summary>
        /// <remarks>
        /// For an agent that should vary beside agents that should not, such as a storyteller at 0.8
        /// beside rulers at 0, on one provider and so one queue (N10), rather than a second provider.
        /// It changes the reply, so it is the caller's choice per call; every number in the build
        /// plan's Appendix A was measured at 0. Null sends exactly the request sent before it existed.
        /// A provider that cannot set a temperature ignores it.
        /// </remarks>
        public float? Temperature;

        /// <summary>
        /// The sampling seed for this call, or null, the default, for none (#266 item 7). Sent by
        /// Ollama as <c>options.seed</c>, so a call at a temperature above 0 can be repeated. Null
        /// sends exactly the request sent before it existed.
        /// </summary>
        public int? Seed;

        /// <summary>
        /// The most tokens the reply may run to, or null, the default, for no cap (#244 item 11).
        /// Sent by Ollama as <c>options.num_predict</c>. Zero or less is a mistake the call throws for.
        /// </summary>
        /// <remarks>
        /// For a reply that runs away: a storyteller call once took 20 s and came back unreadable,
        /// holding the queue meanwhile. Nothing the model reads changes; a reply cut at the cap is
        /// not one JSON object, so it ends as a <see cref="PipelineErrorKind.UnreadableReply"/>, as the
        /// runaway did, only sooner. Set it generously: a decision's reply is a few dozen tokens, a
        /// storyteller's story a hundred or two, so 400 binds only a runaway. Null sends exactly the
        /// request sent before it existed.
        /// </remarks>
        public int? MaxReplyTokens;

        /// <summary>
        /// The caller's own object for this call, which the framework carries and never reads
        /// (DR-018): the conversation a reply belongs to, a turn number, a request id. Null by
        /// default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It is set on <see cref="AgentContext.Correlation"/> of every context made for this
        /// decision, so availability checks, target and observation sources, guards and memory see
        /// it, and on <see cref="DecisionResult.Correlation"/>, pipeline errors included. When the
        /// result is executed, the context <see cref="Agent.Execute(DecisionResult)"/> hands the
        /// handler carries it too, so a handler knows which call it is acting for without anything
        /// written to <see cref="Agent.State"/>, which every call on the agent shares.
        /// </para>
        /// <para>
        /// The object itself is kept, never copied, and never reaches the model or the telemetry.
        /// It says which call this is, never why the agent is asked (DR-008).
        /// </para>
        /// </remarks>
        public object Correlation;

        /// <summary>
        /// The chain of relayed decisions this call belongs to, or null for none, the default (#190,
        /// G4). <see cref="DecisionRelay.RelayAsync"/> sets it; set <see cref="DecisionChain.Start"/>
        /// here to put the decision that begins a chain in it too.
        /// </summary>
        /// <remarks>
        /// Recorded in <see cref="DecisionTelemetry.ChainId"/>, <see cref="DecisionTelemetry.ChainHop"/>
        /// and <see cref="DecisionTelemetry.ChainSource"/>, pipeline errors included, so the decision
        /// feed and <see cref="DecisionLog"/> carry it. It never reaches the prompt or the provider.
        /// </remarks>
        public DecisionChain Chain;

        // The options one call keeps, taken when it is made, so changing the caller's object
        // afterwards changes nothing, the guards' view included (#106). A memberwise copy, so a
        // switch added later is copied without being listed here; only a field holding a mutable
        // object needs copying by hand.
        internal DecideOptions Copy()
        {
            var copy = (DecideOptions)MemberwiseClone();
            copy.Observations = Observations == null ? null : new List<string>(Observations);
            copy.Choices = Choices == null ? null : new List<Choice>(Choices);
            return copy;
        }
    }
}
