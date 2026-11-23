namespace GreyBoxStrategy
{
    /// <summary>
    /// The baseline: any legal move, each as likely as the next, and peace accepted half the time.
    /// Its dice are the game's, keyed by turn and country, so a game with random players is as
    /// repeatable as any other.
    /// </summary>
    public sealed class RandomPlayer : IPlayer
    {
        public string Kind => "random";

        public Move Decide(CountryRequest request)
        {
            var legal = request.Legal;
            string purpose = request.Moment == Moment.Envoy ? "random-envoy:" + request.OfferId
                : request.Moment == Moment.Choice ? "random-choice:" + request.OfferId
                : request.Moment == Moment.Army ? "random-army:" + request.ArmyId
                : request.Writ > 1 ? "random-council:" + request.Writ : "random-council";
            int i = Dice.Int(request.World.Seed, request.Turn, request.CountryId, purpose, 0, legal.Count);
            return legal[i];
        }
    }
}
