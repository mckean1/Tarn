namespace Tarn.Domain;

public sealed partial class GameEngine
{
    private void ResolveSpell(MatchState state, PlayerState owner, SpellCardDefinition spell)
    {
        state.Log($"Resolving spell {spell.Id} {spell.Name}.");

        switch (spell.Id)
        {
            case "SP001":
            {
                var target = SelectTargetedEnemyUnit(state, owner.Id);
                if (target is null)
                {
                    state.Log("Searing Order has no valid target and fizzles.");
                    break;
                }

                DealDamageToUnit(state, target, 2, new DamageCause
                {
                    SourcePlayerId = owner.Id,
                    SourceCardId = spell.Id,
                    IsSpellOrEffect = true,
                    IsTargetedUnitEffect = true,
                });
                break;
            }
            case "SP002":
                foreach (var target in state.GetOpponent(owner.Id).Battlefield.OrderBy(unit => unit.ZoneOrder).ToList())
                {
                    DealDamageToUnit(state, target, 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = spell.Id,
                        IsSpellOrEffect = true,
                    });
                }
                break;
            case "SP003":
                ModifyOldestFriendlyUnit(state, owner.Id, "Rallying Script", unit => unit.Attack += 2);
                break;
            case "SP004":
                ModifyOldestFriendlyUnit(state, owner.Id, "Fortify Line", unit =>
                {
                    unit.Health += 2;
                    unit.HasDefender = true;
                });
                break;
            case "SP005":
                ModifyOldestFriendlyUnit(state, owner.Id, "Guided Coil", unit =>
                {
                    unit.Attack += 1;
                    unit.HasMagnet = true;
                });
                break;
            case "SP006":
                HealChampion(state, owner.Id, 3, "Mending Light");
                break;
            default:
                throw new InvalidOperationException($"Unknown spell {spell.Id}.");
        }

        PerformDeathCheck(state);
        CheckWinCondition(state);
    }

    private void ModifyOldestFriendlyUnit(MatchState state, string playerId, string label, Action<UnitState> apply)
    {
        var target = SelectOldestFriendlyUnit(state, playerId);
        if (target is null)
        {
            state.Log($"{label} has no valid unit target.");
            return;
        }

        apply(target);
        state.Log($"{label} modifies {target.InstanceId}.");
    }

    private void ResolveAttackStep(MatchState state)
    {
        state.Log("Attack Step begins.");

        foreach (var attacker in GetAttackOrder(state))
        {
            if (ShouldStopRound(state))
            {
                return;
            }

            if (attacker is UnitState unit)
            {
                if (unit.IsDead || unit.Attack <= 0 || unit.EnteredRound == state.RoundNumber)
                {
                    continue;
                }

                ResolveUnitAttack(state, unit);
                continue;
            }

            var champion = (ChampionState)attacker;
            var championOwner = state.GetPlayer(champion.OwnerId);
            if (!champion.Card.CanAttack || champion.Attack <= 0)
            {
                continue;
            }

            ResolveChampionAttack(state, championOwner);
        }
    }

    private IEnumerable<object> GetAttackOrder(MatchState state)
    {
        foreach (var player in GetPlayersInSourceOrder(state))
        {
            yield return player.Champion;
            foreach (var unit in player.Battlefield.OrderBy(unit => unit.ZoneOrder))
            {
                yield return unit;
            }
        }
    }

    private void ResolveUnitAttack(MatchState state, UnitState attacker)
    {
        var owner = state.GetPlayer(attacker.OwnerId);
        var defender = state.GetOpponent(owner.Id);

        var pendingAttack = new PendingEffect
        {
            Kind = PendingEffectKind.Attack,
            OwnerId = owner.Id,
            Description = $"{attacker.InstanceId} attacks {defender.Id}'s Champion",
            SourceCardId = attacker.Card.Id,
            Attacker = attacker,
            Action = () => { },
        };

        pendingAttack.Action = () =>
        {
            var attackBonus = ResolveAttackBonuses(state, attacker);
            var damage = Math.Max(0, attacker.Attack + attackBonus);
            state.Log($"{attacker.InstanceId} attacks {defender.Id}'s Champion for {damage}.");
            if (pendingAttack.PreventAttackDamage)
            {
                state.Log($"Brace the Line prevents {attacker.InstanceId}'s damage.");
                return;
            }

            DealDamageToChampion(state, defender, damage, new DamageCause
            {
                SourcePlayerId = owner.Id,
                SourceCardId = attacker.Card.Id,
                IsUnitAttack = true,
            });
        };

        ResolvePendingEffect(state, pendingAttack);
    }

    private void ResolveChampionAttack(MatchState state, PlayerState owner)
    {
        var defender = state.GetOpponent(owner.Id);
        state.Log($"{owner.Id}'s Champion attacks {defender.Id}'s Champion for {owner.Champion.Attack}.");
        DealDamageToChampion(state, defender, owner.Champion.Attack, new DamageCause
        {
            SourcePlayerId = owner.Id,
            SourceCardId = owner.Champion.Card.Id,
        }, ignoreDefender: true);
        PerformDeathCheck(state);
        CheckWinCondition(state);
    }

    private int ResolveAttackBonuses(MatchState state, UnitState attacker)
    {
        var attackBonus = 0;
        ResolveTriggers(state, TriggerType.OnAttack, new TriggerContext { Attacker = attacker }, bonus => attackBonus += bonus);
        return attackBonus;
    }

    private void ApplyFatigue(MatchState state, PlayerState player)
    {
        player.FatigueCount++;
        state.Log($"{player.Id} is out of cards and takes Fatigue {player.FatigueCount}.");

        // TODO: Fatigue is isolated here so the escalation rule can change later without touching the engine core.
        DealDamageToChampion(state, player, player.FatigueCount, new DamageCause
        {
            SourcePlayerId = player.Id,
            SourceCardId = "FATIGUE",
        }, ignoreDefender: true);
        PerformDeathCheck(state);
        CheckWinCondition(state);
    }

    private void DealDamageToChampion(MatchState state, PlayerState targetPlayer, int amount, DamageCause cause, bool ignoreDefender = false)
    {
        if (amount <= 0)
        {
            return;
        }

        var remaining = amount;
        if (!ignoreDefender && cause.IsUnitAttack)
        {
            foreach (var defender in targetPlayer.Battlefield.Where(unit => unit.HasDefender).OrderBy(unit => unit.ZoneOrder).ToList())
            {
                if (remaining <= 0)
                {
                    break;
                }

                var assigned = Math.Min(remaining, Math.Max(0, defender.Health));
                if (assigned <= 0)
                {
                    continue;
                }

                state.Log($"{assigned} attack damage is assigned to Defender {defender.InstanceId}.");
                DealDamageToUnit(state, defender, assigned, cause, performChecks: false);
                remaining -= assigned;
            }
        }

        if (remaining <= 0)
        {
            PerformDeathCheck(state);
            CheckWinCondition(state);
            return;
        }

        targetPlayer.Champion.Health -= remaining;
        targetPlayer.Champion.TookDamageThisRound = true;
        state.GetOpponent(targetPlayer.Id).Champion.EnemyChampionTookDamageThisRound = true;
        state.Log($"{targetPlayer.Id}'s Champion takes {remaining} damage and falls to {targetPlayer.Champion.Health}.");
    }

    private void DealDamageToUnit(MatchState state, UnitState target, int amount, DamageCause cause, bool performChecks = true)
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        target.Health -= amount;
        state.Log($"{target.InstanceId} takes {amount} damage and falls to {target.Health}.");

        ResolveTriggers(state, TriggerType.OnDamage, new TriggerContext
        {
            DamagedUnit = target,
            DamageCause = cause,
        });

        PerformDeathCheck(state);

        if (!target.IsDead)
        {
            ResolveTriggers(state, TriggerType.OnSurvive, new TriggerContext
            {
                DamagedUnit = target,
                DamageCause = cause,
            });
        }

        if (performChecks)
        {
            PerformDeathCheck(state);
            CheckWinCondition(state);
        }
    }

    private void HealChampion(MatchState state, string playerId, int amount, string source)
    {
        var champion = state.GetPlayer(playerId).Champion;
        var healed = Math.Max(0, Math.Min(amount, champion.Card.Health - champion.Health));
        if (healed <= 0)
        {
            state.Log($"{source} has no healing effect.");
            return;
        }

        champion.Health += healed;
        state.Log($"{source} heals {playerId}'s Champion for {healed} to {champion.Health}.");
    }

    private void PerformDeathCheck(MatchState state)
    {
        while (true)
        {
            var deadUnit = state.Players
                .SelectMany(player => player.Battlefield)
                .Where(unit => unit.IsDead && !unit.IsInDeathProcess)
                .OrderBy(unit => unit.ZoneOrder)
                .FirstOrDefault();

            if (deadUnit is null)
            {
                return;
            }

            state.Log($"Death check finds {deadUnit.InstanceId} dead.");
            deadUnit.IsInDeathProcess = true;
            ResolveTriggers(state, TriggerType.OnDeath, new TriggerContext { DeadUnit = deadUnit });

            var owner = state.GetPlayer(deadUnit.OwnerId);
            owner.Battlefield.Remove(deadUnit);
            owner.Discard.Add(deadUnit.Card);
            state.Log($"{deadUnit.InstanceId} leaves the Battlefield and moves to Discard.");

            ResolveTriggers(state, TriggerType.OnDestroyed, new TriggerContext { DeadUnit = deadUnit });
            ResolveDestroyedUnitSelfTrigger(state, deadUnit);

            CheckWinCondition(state);
            if (ShouldStopRound(state))
            {
                return;
            }
        }
    }

    private void ResolveDestroyedUnitSelfTrigger(MatchState state, UnitState deadUnit)
    {
        TriggerSpec? trigger = deadUnit.Card.Id switch
        {
            "UN010" => new TriggerSpec("UN010", $"UN010 On Destroyed: {deadUnit.InstanceId} deals 1 to the enemy Champion.", sink =>
            {
                DealDamageToChampion(state, state.GetOpponent(deadUnit.OwnerId), 1, new DamageCause
                {
                    SourcePlayerId = deadUnit.OwnerId,
                    SourceCardId = "UN010",
                    IsSpellOrEffect = true,
                }, ignoreDefender: true);
            }),
            "UN012" => new TriggerSpec("UN012", $"UN012 On Destroyed: {deadUnit.InstanceId} grants +1 Attack.", sink =>
            {
                var target = SelectOldestFriendlyUnit(state, deadUnit.OwnerId);
                if (target is null)
                {
                    state.Log("Relic Rat has no friendly unit to empower.");
                    return;
                }

                target.Attack += 1;
                state.Log($"{target.InstanceId} gains +1 Attack from Relic Rat.");
            }),
            _ => null,
        };

        if (trigger is null)
        {
            return;
        }

        ResolvePendingEffect(state, new PendingEffect
        {
            Kind = PendingEffectKind.Ability,
            OwnerId = deadUnit.OwnerId,
            Description = trigger.Description,
            SourceCardId = trigger.SourceCardId,
            Action = () => trigger.Resolve(null),
        });
    }
}
