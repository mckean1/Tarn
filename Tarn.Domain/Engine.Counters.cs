namespace Tarn.Domain;

public sealed partial class GameEngine
{
    private void ResolvePendingEffect(MatchState state, PendingEffect rootEffect)
    {
        var stack = new List<PendingEffect> { rootEffect };
        var current = rootEffect;
        var preferredResponder = state.GetOpponent(rootEffect.OwnerId).Id;

        while (true)
        {
            var nextCounter = FindNextCounter(state, preferredResponder, current)
                ?? FindNextCounter(state, state.GetOpponent(preferredResponder).Id, current);

            if (nextCounter is null)
            {
                break;
            }

            state.GetPlayer(nextCounter.OwnerId).CounterZone.Remove(nextCounter);
            state.GetPlayer(nextCounter.OwnerId).Discard.Add(nextCounter.Card);
            state.Log($"{nextCounter.InstanceId} commits from the Counter Zone targeting {current.Description}.");
            var targetEffect = current;

            var counterEffect = new PendingEffect
            {
                Kind = PendingEffectKind.Counter,
                OwnerId = nextCounter.OwnerId,
                Description = $"{nextCounter.InstanceId} counter effect",
                SourceCardId = nextCounter.Card.Id,
                Targets = targetEffect,
                CounterCard = nextCounter,
                Action = () => ResolveCounterEffect(state, nextCounter, targetEffect),
            };

            stack.Add(counterEffect);
            current = counterEffect;
            preferredResponder = state.GetOpponent(nextCounter.OwnerId).Id;
        }

        for (var index = stack.Count - 1; index >= 0; index--)
        {
            var effect = stack[index];
            if (effect.IsCountered)
            {
                state.Log($"{effect.Description} is countered.");
                ResolveTriggers(state, TriggerType.OnCountered, new TriggerContext
                {
                    CounteredEffectOwnerId = effect.OwnerId,
                });
                continue;
            }

            state.Log($"Resolve: {effect.Description}");
            effect.Action();
            PerformDeathCheck(state);
            CheckWinCondition(state);
            if (ShouldStopRound(state))
            {
                return;
            }
        }
    }

    private CounterState? FindNextCounter(MatchState state, string playerId, PendingEffect current)
    {
        return state.GetPlayer(playerId).CounterZone
            .OrderBy(counter => counter.ZoneOrder)
            .FirstOrDefault(counter => IsValidCounter(counter, current));
    }

    private static bool IsValidCounter(CounterState counter, PendingEffect target)
    {
        if (string.Equals(counter.OwnerId, target.OwnerId, StringComparison.Ordinal))
        {
            return false;
        }

        return counter.Card.Trigger switch
        {
            CounterTriggerType.EnemySpellWouldResolve => target.Kind == PendingEffectKind.Spell,
            CounterTriggerType.EnemyAbilityWouldResolve => target.Kind == PendingEffectKind.Ability,
            CounterTriggerType.EnemyCounterWouldResolve => target.Kind == PendingEffectKind.Counter,
            CounterTriggerType.EnemyUnitAttacks => target.Kind == PendingEffectKind.Attack && target.Attacker is not null,
            _ => false,
        };
    }

    private static void ResolveCounterEffect(MatchState state, CounterState counter, PendingEffect target)
    {
        switch (counter.Card.Id)
        {
            case "CT001":
            case "CT002":
            case "CT003":
                target.IsCountered = true;
                state.Log($"{counter.Card.Id} counters {target.Description}.");
                break;
            case "CT004":
                if (target.Kind != PendingEffectKind.Attack || target.Attacker is null)
                {
                    state.Log("Brace the Line has no valid attack to stop.");
                    return;
                }

                target.PreventAttackDamage = true;
                state.Log($"Brace the Line prevents damage from {target.Attacker.InstanceId} this attack.");
                break;
            default:
                throw new InvalidOperationException($"Unknown counter {counter.Card.Id}.");
        }
    }

    private void CheckWinCondition(MatchState state)
    {
        if (state.WinnerPlayerId is not null || state.OvertimePending)
        {
            return;
        }

        var firstDead = state.PlayerOne.Champion.Health <= 0;
        var secondDead = state.PlayerTwo.Champion.Health <= 0;

        state.Log($"Win check: {state.PlayerOne.Id}={state.PlayerOne.Champion.Health}, {state.PlayerTwo.Id}={state.PlayerTwo.Champion.Health}.");

        if (!firstDead && !secondDead)
        {
            return;
        }

        if (firstDead && secondDead)
        {
            state.OvertimePending = true;
            state.Log("Both Champions are at 0 or less. Enter Overtime.");
            return;
        }

        state.WinnerPlayerId = firstDead ? state.PlayerTwo.Id : state.PlayerOne.Id;
    }

    private void BeginOvertime(MatchState state)
    {
        state.OvertimePending = false;
        state.Log("Overtime reset: both Champions return to 1 Health.");

        // TODO: Overtime is isolated here because the exact official reset policy is still ambiguous.
        state.PlayerOne.Champion.Health = 1;
        state.PlayerTwo.Champion.Health = 1;
    }
}
