using System;
using System.Collections.Generic;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// An agent's memory as plain data, from <see cref="IExportsMemory.Export"/>, for a save file
    /// (DR-018). Public fields and lists only, so <c>JsonUtility</c> and Newtonsoft both store it as
    /// it is, inside the game's own save object.
    /// </summary>
    /// <remarks>
    /// <see cref="Exchanges"/> use the encoding every strategy shares, the one
    /// <see cref="DecisionRequest.History"/> fixes. The rest of a strategy's state goes in
    /// <see cref="Data"/>. <see cref="Format"/> and <see cref="Version"/> name the strategy that
    /// wrote it, and a strategy imports only its own: <see cref="RollingHistory.Import"/> refuses any
    /// other, and <see cref="RollingHistory.CanImport"/> says so first.
    /// </remarks>
    [Serializable]
    public sealed class MemorySnapshot
    {
        /// <summary>Which strategy wrote it, such as <see cref="RollingHistory.SnapshotFormat"/>.</summary>
        public string Format;

        /// <summary>The version of that strategy's <see cref="Data"/>, its own to number.</summary>
        public int Version;

        /// <summary>The exchanges remembered, oldest first.</summary>
        public List<MemoryExchange> Exchanges = new List<MemoryExchange>();

        /// <summary>
        /// Anything else the strategy keeps, in its own encoding, such as JSON; null when it keeps
        /// nothing more. <see cref="RollingHistory"/> keeps nothing here.
        /// </summary>
        /// <remarks>
        /// <c>JsonUtility</c> writes a null string as an empty one, so a strategy reads empty as
        /// nothing too.
        /// </remarks>
        public string Data;
    }

    /// <summary>
    /// One remembered exchange, as <see cref="DecisionRequest.History"/> holds it: the stimulus as it
    /// was sent, label included, then the decision as the schema's JSON.
    /// </summary>
    [Serializable]
    public sealed class MemoryExchange
    {
        /// <summary>The stimulus as the model read it, such as <c>Player: "Follow me."</c>.</summary>
        public string Stimulus;

        /// <summary>The decision as one line of JSON, in the schema's field order.</summary>
        public string Decision;

        /// <summary>An empty exchange, for serializers.</summary>
        public MemoryExchange()
        {
        }

        public MemoryExchange(string stimulus, string decision)
        {
            Stimulus = stimulus;
            Decision = decision;
        }
    }
}
