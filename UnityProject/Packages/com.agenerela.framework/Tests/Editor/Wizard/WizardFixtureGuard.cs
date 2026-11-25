using Agenerela;
using UnityEngine;

// Written by Create > Agenerela > New Agent. It is yours to edit: fill in each TODO.
public class WizardFixtureGuard : MonoBehaviour
{
    [AgentAction("move_to", "Walk to a named place.", RequiresTarget = true)]
    [Example("Keep watch over by the {0}.")]
    void MoveTo(AgentDecision decision)
    {
        // TODO: what move_to does in your game.
        Debug.Log($"{name}: move_to, target {decision.TargetId}", this);
    }

    // TODO: return false while this action makes no sense, so the agent is not offered it.
    [Available("move_to")]
    bool CanMoveTo() => true;

    [AgentAction("follow_player", "Walk with whoever asked.", TakesNoTarget = true)]
    [Example("Stay close, I'm heading out.")]
    void FollowPlayer(AgentDecision decision)
    {
        // TODO: what follow_player does in your game.
        Debug.Log($"{name}: follow_player, target {decision.TargetId}", this);
    }

    // TODO: return false while this action makes no sense, so the agent is not offered it.
    [Available("follow_player")]
    bool CanFollowPlayer() => true;

    [AgentAction("speak_only", "Answer without moving.", TakesNoTarget = true)]
    [Example("What's the news today?")]
    void SpeakOnly(AgentDecision decision)
    {
        // TODO: what speak_only does in your game.
        Debug.Log($"{name}: speak_only, target {decision.TargetId}", this);
    }

    // TODO: return false while this action makes no sense, so the agent is not offered it.
    [Available("speak_only")]
    bool CanSpeakOnly() => true;

    [AgentAction("attack_target", "Strike a named thing.", RequiresTarget = true)]
    [Example("Drive off the {0}!")]
    void AttackTarget(AgentDecision decision)
    {
        // TODO: what attack_target does in your game.
        Debug.Log($"{name}: attack_target, target {decision.TargetId}", this);
    }

    // TODO: return false while this action makes no sense, so the agent is not offered it.
    [Available("attack_target")]
    bool CanAttackTarget() => true;

    [AgentAction("wave", "Wave at someone, \"friendly\" like.")]
    // TODO: [Example("something a player might say that leads to this action")]
    void Wave(AgentDecision decision)
    {
        // TODO: what wave does in your game.
        Debug.Log($"{name}: wave, target {decision.TargetId}", this);
    }

    // TODO: return false while this action makes no sense, so the agent is not offered it.
    [Available("wave")]
    bool CanWave() => true;
}
