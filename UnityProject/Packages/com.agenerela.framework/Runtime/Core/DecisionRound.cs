using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Optional plumbing for asking several agents on one snapshot and then acting on their answers
    /// in an order chosen afterwards (#216, item 2; DR-021): a turn of a strategy game, a colony's
    /// dawn, a crew's answer to one order. <see cref="DecideAsync"/> decides every agent through the
    /// normal queue and returns once all have answered, with nothing executed; the caller reads the
    /// results, picks the order, and <see cref="Execute(Agent[])"/> carries the decisions out in it,
    /// each re-checked by <see cref="Agent.Execute(DecisionResult, out ActionResult)"/>, so a move an
    /// earlier one made illegal is refused and reported as <see cref="RoundEntry.Stale"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The order comes after the decisions.</b> A game whose order depends on what was chosen,
    /// GreyBoxStrategy's peace first, then home moves, wars and attacks, with seats rotating, can
    /// only give it once it has seen the results, so the round takes it then, never up front.
    /// <see cref="Execute(Agent[])"/> may be called as often as the game has phases: each call runs
    /// the agents it names, in its order, and none of them is decided again. A round knows nothing
    /// of phases, and never decides when it happens or why the agents are asked (DR-008): the game
    /// calls it, with the stimulus and options it would give <see cref="Agent.DecideAsync"/>.
    /// </para>
    /// <para>
    /// <b>Memory.</b> An agent with a <see cref="Agent.Memory"/> must remember on execution
    /// (<see cref="Agenerela.MemoryCommit.OnExecute"/>), or the round throws before asking anyone: under
    /// <see cref="Agenerela.MemoryCommit.OnDecide"/> an answer would be remembered before the round
    /// could drop it. So a decision enters memory only when <see cref="Execute(Agent[])"/> carries it
    /// out, as <see cref="Agent.Execute(DecisionResult)"/> always remembers one; one refused as stale,
    /// or never executed, is not. <see cref="End"/> discards every decision still waiting, so a later
    /// <see cref="Agent.Execute(DecisionResult)"/> or <see cref="Agent.Commit"/> of it remembers
    /// nothing either.
    /// </para>
    /// <para>
    /// <b>Cancellation drops the whole round.</b> Cancelling the token while any agent is still
    /// deciding cancels every request still out, discards from memory every answer already in, and
    /// ends <see cref="DecideAsync"/> with an <see cref="OperationCanceledException"/>, at once, even
    /// when a provider ignores the cancellation: its answer is discarded when it comes. One agent's
    /// trouble is its own: a pipeline error is that entry's result, and a call that throws leaves
    /// that entry <see cref="RoundEntryStatus.Failed"/> and the others decided. So does an
    /// <see cref="Agent.Reset"/> of one agent at any time before the round returns, before or after
    /// that agent answered: its decision is dropped, never executed.
    /// </para>
    /// <para>
    /// <b>The log.</b> Each decision is an ordinary <see cref="Agent.DecideAsync"/> call, so
    /// <see cref="DecisionLog"/> and <see cref="DecisionFeed"/> record it as any other, and each
    /// <see cref="Execute(Agent[])"/> as an <see cref="DecisionStage.Executed"/> record. To group a
    /// round's decisions, each call whose options carry no <see cref="DecideOptions.Chain"/> is given
    /// <c>DecisionChain.Start(round.Id)</c>, so its log line carries the round's id as its
    /// <c>chainId</c>, at hop 0, and a decision relayed from it (<see cref="DecisionRelay"/>) stays in
    /// that chain. A call with a chain of its own keeps it. <see cref="DecideOptions.Correlation"/> is
    /// never read or set: it stays the caller's.
    /// </para>
    /// <para>
    /// Plain C# with no scene. Use it on the main thread, as agents are used.
    /// </para>
    /// <para>
    /// <b>Extension point:</b> a broadcast, one stimulus to a crew whose answers are merged (#216,
    /// F8), would be a mode beside this one, once a demo needs it. A per-round deadline is the
    /// caller's token today, cancelled by a timer of its own (F11).
    /// </para>
    /// </remarks>
    public sealed class DecisionRound
    {
        private readonly List<RoundEntry> entries;
        private readonly Dictionary<Agent, RoundEntry> byAgent;
        private bool executing;

        private DecisionRound(string id, List<RoundEntry> entries)
        {
            Id = id;
            this.entries = entries;
            Entries = new ReadOnlyCollection<RoundEntry>(entries);
            byAgent = new Dictionary<Agent, RoundEntry>();
            foreach (var entry in entries)
            {
                byAgent.Add(entry.Agent, entry);
            }
        }

        /// <summary>
        /// The round's id: the one given to <see cref="DecideAsync"/>, or a new one. It is the
        /// <see cref="DecisionChain.Id"/> of every call the round gave a chain.
        /// </summary>
        public string Id { get; }

        /// <summary>Every agent's entry, in the order the calls were given.</summary>
        public IReadOnlyList<RoundEntry> Entries { get; }

        /// <summary>The entries still waiting to be executed (<see cref="RoundEntryStatus.Decided"/>), in call order. A copy.</summary>
        public IReadOnlyList<RoundEntry> Waiting
        {
            get
            {
                var waiting = new List<RoundEntry>();
                foreach (var entry in entries)
                {
                    if (entry.Status == RoundEntryStatus.Decided)
                    {
                        waiting.Add(entry);
                    }
                }

                return waiting;
            }
        }

        /// <summary>Whether <see cref="End"/> has ended the round, after which nothing more is executed.</summary>
        public bool IsEnded { get; private set; }

        /// <summary>The entry of <paramref name="agent"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="agent"/> is not in this round, or is null.</exception>
        public RoundEntry this[Agent agent] => Find(agent, nameof(agent));

        /// <summary>Whether <paramref name="agent"/> is in this round.</summary>
        public bool Contains(Agent agent)
        {
            return agent != null && byAgent.ContainsKey(agent);
        }

        /// <summary>
        /// Decides every call through <see cref="Agent.DecideAsync"/>, all started at once into the
        /// normal queue, and returns the round once every one has answered, with nothing executed.
        /// </summary>
        /// <param name="calls">One per agent, each agent at most once; none makes an empty round, decided at once, as a turn with no envoy to ask has. The order is the order the calls are made, and so the queue's among equal priorities; it says nothing about the order of execution.</param>
        /// <param name="id">The round's id, such as <c>"turn-7-council"</c>; null or empty for a new one.</param>
        /// <param name="ct">Cancels the whole round while it decides; once the round is returned, it changes nothing.</param>
        /// <returns>The decided round: every entry <see cref="RoundEntryStatus.Decided"/>, or <see cref="RoundEntryStatus.Failed"/> for a call that threw.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="calls"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="calls"/> holds a call with no agent, names one agent twice, or
        /// has options <see cref="Agent.DecideAsync"/> would throw for (an empty
        /// <see cref="DecideOptions.StimulusLabel"/>, a negative <see cref="DecideOptions.MaxListedTargets"/>).
        /// Thrown at the call, before anyone is asked.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// An agent has a <see cref="Agent.Memory"/> under <see cref="Agenerela.MemoryCommit.OnDecide"/>.
        /// Thrown at the call, before anyone is asked.
        /// </exception>
        /// <exception cref="OperationCanceledException">Awaiting it, when <paramref name="ct"/> was cancelled before every agent answered. Nothing from the round is remembered.</exception>
        public static Awaitable<DecisionRound> DecideAsync(IEnumerable<RoundCall> calls, string id = null, CancellationToken ct = default)
        {
            var round = Prepare(calls, id);
            return round.Decide(ct);
        }

        /// <summary>
        /// Executes these agents' decisions, in this order, each through
        /// <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> and its re-check, and returns
        /// their entries in the same order, each with its <see cref="RoundEntry.Outcome"/> and
        /// <see cref="RoundEntry.Stale"/>. Call it once with every agent, or once per phase.
        /// </summary>
        /// <param name="order">Agents of this round still <see cref="RoundEntryStatus.Decided"/>, each at most once. An agent the game leaves out stays waiting, for a later call or <see cref="End"/>.</param>
        /// <remarks>
        /// The whole order is checked before anything runs, so a mistake in it executes nothing.
        /// A handler that throws stops the call: its entry becomes <see cref="RoundEntryStatus.Threw"/>
        /// and the exception passes on, as <see cref="Agent.Execute(DecisionResult)"/> lets it; the
        /// entries before it keep their outcomes, and those after it stay waiting.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="order"/> is null.</exception>
        /// <exception cref="ArgumentException">An agent in it is null, is not in this round, or is named twice.</exception>
        /// <exception cref="InvalidOperationException">
        /// An agent in it is not waiting: already executed, failed, threw or skipped. Or the round has
        /// ended, or this is called from inside a handler the round is running.
        /// </exception>
        public IReadOnlyList<RoundEntry> Execute(IEnumerable<Agent> order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            CheckNotBusy();

            var run = new List<RoundEntry>();
            var seen = new HashSet<RoundEntry>();
            foreach (var agent in order)
            {
                var entry = Find(agent, nameof(order));
                if (!seen.Add(entry))
                {
                    throw new ArgumentException($"{Who(entry)} is named twice in the order: a round executes each decision once.", nameof(order));
                }

                if (entry.Status != RoundEntryStatus.Decided)
                {
                    throw new InvalidOperationException(
                        $"{Who(entry)} is not waiting to be executed: it is {entry.Status}. A round executes each decision once, and only a decided one.");
                }

                run.Add(entry);
            }

            executing = true;
            try
            {
                foreach (var entry in run)
                {
                    Run(entry);
                }
            }
            finally
            {
                executing = false;
            }

            return run.AsReadOnly();
        }

        /// <summary>Executes these agents' decisions in this order, as <see cref="Execute(IEnumerable{Agent})"/> does.</summary>
        public IReadOnlyList<RoundEntry> Execute(params Agent[] order)
        {
            return Execute((IEnumerable<Agent>)order);
        }

        /// <summary>
        /// Ends the round: every entry still waiting is <see cref="RoundEntryStatus.Skipped"/>, its
        /// decision discarded from memory (<see cref="Agent.Discard"/>), and nothing more is executed.
        /// Returns how many were skipped. Ending an ended round does nothing.
        /// </summary>
        /// <remarks>
        /// Not needed for memory's sake: a decision never executed is never remembered unless the game
        /// commits it. It is for a game that keeps the results around, so that nothing carries one out
        /// later by mistake, and for the log's sake, to say which were never acted on.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Called from inside a handler the round is running.</exception>
        public int End()
        {
            if (executing)
            {
                throw new InvalidOperationException("A round cannot be ended from inside a handler it is running.");
            }

            if (IsEnded)
            {
                return 0;
            }

            IsEnded = true;
            int skipped = 0;
            foreach (var entry in entries)
            {
                if (entry.Status != RoundEntryStatus.Decided)
                {
                    continue;
                }

                entry.Status = RoundEntryStatus.Skipped;
                entry.Agent.Discard(entry.Result);
                skipped++;
            }

            return skipped;
        }

        // ---- Deciding -------------------------------------------------------------------------

        private static DecisionRound Prepare(IEnumerable<RoundCall> calls, string id)
        {
            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            id = string.IsNullOrEmpty(id) ? DecisionChain.NewId() : id;
            var list = new List<RoundEntry>();
            var agents = new HashSet<Agent>();
            foreach (var call in calls)
            {
                var agent = call.Agent;
                if (agent == null)
                {
                    throw new ArgumentException("A call has no agent: build each with new RoundCall(agent, stimulus, options).", nameof(calls));
                }

                if (!agents.Add(agent))
                {
                    throw new ArgumentException($"{Name(agent)} is asked twice in one round. A round asks each agent once; ask it again in the next.", nameof(calls));
                }

                if (agent.Memory != null && agent.MemoryCommit != MemoryCommit.OnExecute)
                {
                    throw new InvalidOperationException(
                        $"{Name(agent)} remembers each decision as DecideAsync returns it (MemoryCommit.OnDecide), so a round that is " +
                        "cancelled, or a decision it never executes, would be remembered anyway. " +
                        "Set agent.MemoryCommit = MemoryCommit.OnExecute, or agent.Memory = null for an agent with no memory.");
                }

                var options = call.Options == null ? new DecideOptions() : call.Options.Copy();
                CheckOptions(options, agent);
                if (options.Chain == null)
                {
                    options.Chain = DecisionChain.Start(id);
                }

                list.Add(new RoundEntry(list.Count, agent, call.Stimulus, options));
            }

            return new DecisionRound(id, list);
        }

        // DecideAsync's own checks at the call, made for every agent before any is asked, so a
        // mistake in the last call costs the first nothing.
        private static void CheckOptions(DecideOptions options, Agent agent)
        {
            if (string.IsNullOrWhiteSpace(options.StimulusLabel))
            {
                throw new ArgumentException(
                    $"The call for {Name(agent)} has an empty DecideOptions.StimulusLabel. It labels the stimulus; the default is \"Player\".",
                    "calls");
            }

            if (options.MaxListedTargets < 0)
            {
                throw new ArgumentOutOfRangeException("calls", options.MaxListedTargets,
                    $"The call for {Name(agent)} has a negative DecideOptions.MaxListedTargets. 0 lists no targets.");
            }

            if (!(options.TargetGroundingThreshold >= 0f && options.TargetGroundingThreshold <= 1f))
            {
                throw new ArgumentOutOfRangeException("calls", options.TargetGroundingThreshold,
                    $"The call for {Name(agent)} has a DecideOptions.TargetGroundingThreshold outside 0 to 1. The default is 0.8; 0 never stops a decision.");
            }
        }

        private async Awaitable<DecisionRound> Decide(CancellationToken ct)
        {
            if (entries.Count == 0)
            {
                ct.ThrowIfCancellationRequested();
                return this;
            }

            // One token for every request, cancelled with the caller's or when the round is dropped,
            // and disposed once every request has ended (Codex, round 1).
            var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var decisions = new Task<DecisionResult>[entries.Count];

            // Each agent's reset token as the round asks it: an agent reset at any point before the
            // round returns has its part dropped, answered or not (Codex, round 1).
            var resets = new CancellationToken[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                resets[i] = entry.Agent.ResetToken;
                try
                {
                    decisions[i] = AsTask(entry.Agent.DecideAsync(entry.Stimulus, entry.Options, stop.Token));
                }
                catch (Exception e)
                {
                    // A mistake this agent's DecideAsync found at the call that Prepare did not.
                    decisions[i] = Task.FromException<DecisionResult>(e);
                }
            }

            // Waits for every call however it ends, so a failed one is read below, never thrown here.
            var quiet = new Task[decisions.Length];
            for (int i = 0; i < decisions.Length; i++)
            {
                quiet[i] = Quiet(decisions[i]);
            }

            var all = Task.WhenAll(quiet);
            if (!all.IsCompleted && !ct.IsCancellationRequested)
            {
                // Raced against the caller's token, so a provider that ignores cancellation cannot
                // keep a dropped round waiting.
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (ct.Register(() => cancelled.TrySetResult(true)))
                {
                    await Task.WhenAny(all, cancelled.Task);
                }
            }

            if (ct.IsCancellationRequested)
            {
                stop.Cancel();
                for (int i = 0; i < entries.Count; i++)
                {
                    _ = DiscardWhenDone(entries[i].Agent, decisions[i]);
                }

                // Only once the round is done with it, and every request has ended (Codex, round 2).
                _ = DisposeWhenDone(stop, all);

                throw new OperationCanceledException("The round was cancelled before every agent answered; nothing from it is remembered.", ct);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var decision = decisions[i];
                if (decision.Status == TaskStatus.RanToCompletion && decision.Result != null && resets[i].IsCancellationRequested)
                {
                    // Answered, then the agent was reset while the others were still deciding: the
                    // reset dropped its held memory already; the round drops the decision too.
                    entry.Agent.Discard(decision.Result);
                    entry.Error = new OperationCanceledException($"{Who(entry)} was reset after it answered, before the round was decided.", resets[i]);
                    entry.Status = RoundEntryStatus.Failed;
                }
                else if (decision.Status == TaskStatus.RanToCompletion && decision.Result != null)
                {
                    entry.Result = decision.Result;
                    entry.Status = RoundEntryStatus.Decided;
                }
                else
                {
                    entry.Error = decision.IsFaulted
                        ? decision.Exception.GetBaseException()
                        : new OperationCanceledException($"{Who(entry)}'s decision was cancelled, though the round was not: the agent was reset.");
                    entry.Status = RoundEntryStatus.Failed;
                }
            }

            // Every request has ended, so nothing holds the token any more.
            stop.Dispose();
            return this;
        }

        // A dropped round's decision, discarded from memory once it is in: at once for one that has
        // answered, later for one still out. Never throws: nothing awaits it.
        private static async Task DiscardWhenDone(Agent agent, Task<DecisionResult> decision)
        {
            DecisionResult result = null;
            try
            {
                result = await decision;
            }
            catch (Exception)
            {
                // Cancelled, as a dropped request should be, or failed: nothing to remember either way.
            }

            if (result == null)
            {
                return;
            }

            try
            {
                agent.Discard(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static async Task DisposeWhenDone(CancellationTokenSource source, Task every)
        {
            try
            {
                await every;
            }
            finally
            {
                source.Dispose();
            }
        }

        private static async Task Quiet(Task decision)
        {
            try
            {
                await decision;
            }
            catch (Exception)
            {
                // Read from the decision itself, per entry.
            }
        }

        private static async Task<DecisionResult> AsTask(Awaitable<DecisionResult> awaitable)
        {
            return await awaitable;
        }

        // ---- Executing ------------------------------------------------------------------------

        private void Run(RoundEntry entry)
        {
            ActionResult outcome;
            try
            {
                entry.Agent.Execute(entry.Result, out outcome);
            }
            catch (Exception e)
            {
                entry.Error = e;
                entry.Status = RoundEntryStatus.Threw;
                throw;
            }

            entry.Outcome = outcome;
            entry.Stale = IsStale(entry.Result, outcome);
            entry.Status = RoundEntryStatus.Executed;
        }

        // Refused by Execute's re-check although DecideAsync accepted it: no pipeline error, and no
        // guard rejected the reply.
        private static bool IsStale(DecisionResult result, ActionResult outcome)
        {
            return outcome.Status == ExecutionStatus.Refused
                && result.Decision != null
                && result.Telemetry.Outcome != DecisionOutcome.PipelineError
                && !(result.Verdict.HasValue && result.Verdict.Value.Kind == GuardVerdictKind.Reject);
        }

        private void CheckNotBusy()
        {
            if (executing)
            {
                throw new InvalidOperationException(
                    "A round cannot execute from inside a handler it is running: return from the handler, then execute the next phase.");
            }

            if (IsEnded)
            {
                throw new InvalidOperationException("The round has ended: End() skipped every decision still waiting.");
            }
        }

        private RoundEntry Find(Agent agent, string parameter)
        {
            if (agent == null)
            {
                throw new ArgumentException("An agent in the order is null.", parameter);
            }

            if (!byAgent.TryGetValue(agent, out var entry))
            {
                throw new ArgumentException($"{Name(agent)} is not in this round.", parameter);
            }

            return entry;
        }

        private static string Who(RoundEntry entry)
        {
            return Name(entry.Agent);
        }

        private static string Name(Agent agent)
        {
            string name = agent.Id ?? agent.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? "An agent" : $"Agent '{name}'";
        }
    }
}
