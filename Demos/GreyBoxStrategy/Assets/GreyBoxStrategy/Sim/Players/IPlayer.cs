namespace GreyBoxStrategy
{
    /// <summary>
    /// Whatever decides for one country: the scripted AI, the random baseline, and in stage 2 a
    /// framework <c>Agent</c> behind an adapter. Each call is given one request and returns one of
    /// <see cref="CountryRequest.Legal"/>.
    /// </summary>
    /// <remarks>
    /// The interface is synchronous because a script answers at once. A model does not: stage 2
    /// drives <see cref="Game"/>'s three calls itself and awaits every country's <c>DecideAsync</c>
    /// between them, on the same snapshot, so it needs no change here. A choice not in
    /// <see cref="CountryRequest.Legal"/> is held instead and logged as refused.
    /// </remarks>
    public interface IPlayer
    {
        /// <summary>Short and stable, for the summary table: "expansionist", "random".</summary>
        string Kind { get; }

        /// <summary>
        /// Answers a <see cref="Moment.Council"/> request with one move, or a
        /// <see cref="Moment.Envoy"/> request with <c>accept_peace</c> or <c>refuse_peace</c>.
        /// </summary>
        Move Decide(CountryRequest request);
    }
}
