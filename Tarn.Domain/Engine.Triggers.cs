namespace Tarn.Domain;

public sealed partial class GameEngine
{
    private void ResolveTriggers(MatchState state, TriggerType triggerType, TriggerContext context, Action<int>? attackBonusSink = null)
    {
        foreach (var player in GetPlayersInSourceOrder(state))
        {
            foreach (var source in GetSourcesForPlayer(player))
            {
                foreach (var trigger in GetTriggerSpecs(state, player, source, triggerType, context))
                {
                    ResolvePendingEffect(state, new PendingEffect
                    {
                        Kind = PendingEffectKind.Ability,
                        OwnerId = player.Id,
                        Description = trigger.Description,
                        SourceCardId = trigger.SourceCardId,
                        Action = () => trigger.Resolve(attackBonusSink),
                    });

                    if (ShouldStopRound(state))
                    {
                        return;
                    }
                }
            }
        }
    }

    private IEnumerable<PlayerState> GetPlayersInSourceOrder(MatchState state)
    {
        yield return state.GetPlayer(state.InitiativePlayerIndex);
        yield return state.GetPlayer(1 - state.InitiativePlayerIndex);
    }

    private static IEnumerable<object> GetSourcesForPlayer(PlayerState player)
    {
        yield return player.Champion;
        foreach (var unit in player.Battlefield.OrderBy(unit => unit.ZoneOrder))
        {
            yield return unit;
        }

        foreach (var counter in player.CounterZone.OrderBy(counter => counter.ZoneOrder))
        {
            yield return counter;
        }
    }

    private IEnumerable<TriggerSpec> GetTriggerSpecs(MatchState state, PlayerState owner, object source, TriggerType triggerType, TriggerContext context)
    {
        return source switch
        {
            ChampionState champion => GetChampionTriggers(state, owner, champion, triggerType, context),
            UnitState unit => GetUnitTriggers(state, owner, unit, triggerType, context),
            _ => [],
        };
    }

    private IEnumerable<TriggerSpec> GetChampionTriggers(MatchState state, PlayerState owner, ChampionState champion, TriggerType triggerType, TriggerContext context)
    {
        switch (champion.Card.Id)
        {
            case "CH001" when triggerType == TriggerType.EndOfRound && champion.EnemyChampionTookDamageThisRound:
                yield return new("CH001", "CH001 End of Round: Veyn deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "CH001",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "CH002" when triggerType == TriggerType.OnPlay && context.PlayedByPlayerId == owner.Id && context.PlayedUnit is not null:
                yield return new("CH002", "CH002 On Play: Serah grants +1 Attack.", sink =>
                {
                    context.PlayedUnit.Attack += 1;
                    state.Log($"{context.PlayedUnit.InstanceId} gains +1 Attack from Serah.");
                });
                break;
            case "CH003" when triggerType == TriggerType.StartOfRound:
                yield return new("CH003", "CH003 Start of Round: Garruk grants Defender.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id);
                    if (target is null)
                    {
                        state.Log("Garruk has no unit to grant Defender.");
                        return;
                    }

                    target.HasDefender = true;
                    state.Log($"{target.InstanceId} gains Defender from Garruk.");
                });
                break;
            case "CH004" when triggerType == TriggerType.StartOfRound:
                yield return new("CH004", "CH004 Start of Round: Lyra grants Magnet.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id);
                    if (target is null)
                    {
                        state.Log("Lyra has no unit to grant Magnet.");
                        return;
                    }

                    target.HasMagnet = true;
                    state.Log($"{target.InstanceId} gains Magnet from Lyra.");
                });
                break;
            case "CH005" when triggerType == TriggerType.OnDeath && context.DeadUnit is not null && context.DeadUnit.OwnerId == owner.Id:
                yield return new("CH005", "CH005 On Death: Morcant empowers the oldest friendly unit.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id, context.DeadUnit.InstanceId);
                    if (target is null)
                    {
                        state.Log("Morcant has no unit to empower.");
                        return;
                    }

                    target.Attack += 1;
                    target.Health += 1;
                    state.Log($"{target.InstanceId} gains +1/+1 from Morcant.");
                });
                break;
            case "CH006" when triggerType == TriggerType.OnDestroyed && context.DeadUnit is not null && context.DeadUnit.OwnerId != owner.Id:
                yield return new("CH006", "CH006 On Destroyed: Selka deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "CH006",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "CH007" when triggerType == TriggerType.OnCountered && context.CounteredEffectOwnerId == state.GetOpponent(owner.Id).Id:
                yield return new("CH007", "CH007 On Countered: Noct deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "CH007",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "CH008" when triggerType == TriggerType.StartOfRound && owner.CounterZone.Count > 0:
                yield return new("CH008", "CH008 Start of Round: Aster grants +1 Health.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id);
                    if (target is null)
                    {
                        state.Log("Aster has no unit to bolster.");
                        return;
                    }

                    target.Health += 1;
                    state.Log($"{target.InstanceId} gains +1 Health from Aster.");
                });
                break;
            case "CH009" when triggerType == TriggerType.OnDamage && context.DamagedUnit is not null && context.DamagedUnit.OwnerId == owner.Id && context.DamagedUnit.HasDefender && context.DamageCause?.IsUnitAttack == true:
                yield return new("CH009", "CH009 On Damage: Toma heals the Champion.", sink => HealChampion(state, owner.Id, 1, "Toma, Bulwark Leech"));
                break;
            case "CH010" when triggerType == TriggerType.OnAttack && context.Attacker is not null && context.Attacker.OwnerId == owner.Id:
                yield return new("CH010", "CH010 On Attack: Kael grants +1 Attack for this attack.", sink =>
                {
                    sink?.Invoke(1);
                    state.Log($"{context.Attacker.InstanceId} gains +1 Attack for this attack from Kael.");
                });
                break;
            case "CH011" when triggerType == TriggerType.OnSurvive && context.DamagedUnit is not null && context.DamagedUnit.OwnerId == owner.Id:
                yield return new("CH011", "CH011 On Survive: Brin grants +1 Attack.", sink =>
                {
                    context.DamagedUnit.Attack += 1;
                    state.Log($"{context.DamagedUnit.InstanceId} gains +1 Attack from Brin.");
                });
                break;
            case "CH012" when triggerType == TriggerType.OnDamage && context.DamagedUnit is not null && context.DamagedUnit.OwnerId == owner.Id && context.DamageCause?.IsSpellOrEffect == true:
                yield return new("CH012", "CH012 On Damage: Sable deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "CH012",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "CH013" when triggerType == TriggerType.EndOfRound && owner.Battlefield.Count == 0:
                yield return new("CH013", "CH013 End of Round: Edda heals 2.", sink => HealChampion(state, owner.Id, 2, "Edda, Last Hearth"));
                break;
            case "CH014" when triggerType == TriggerType.OnPlay && context.PlayedByPlayerId == owner.Id && context.PlayedCard?.Type == CardType.Spell:
                yield return new("CH014", "CH014 On Play: Irix deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "CH014",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "CH016" when triggerType == TriggerType.StartOfRound:
                yield return new("CH016", "CH016 Start of Round: Nema grants +1/+1.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id);
                    if (target is null)
                    {
                        state.Log("Nema has no unit to craft.");
                        return;
                    }

                    target.Attack += 1;
                    target.Health += 1;
                    state.Log($"{target.InstanceId} gains +1/+1 from Nema.");
                });
                break;
            case "CH017" when triggerType == TriggerType.OnDestroyed && context.DeadUnit is not null && context.DeadUnit.OwnerId == owner.Id:
                yield return new("CH017", "CH017 On Destroyed: Orren heals the Champion.", sink => HealChampion(state, owner.Id, 1, "Orren, Pale Warden"));
                break;
            case "CH018" when triggerType == TriggerType.StartOfRound && owner.Battlefield.Count < state.GetOpponent(owner.Id).Battlefield.Count:
                yield return new("CH018", "CH018 Start of Round: Vale grants +2 Attack.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id);
                    if (target is null)
                    {
                        state.Log("Vale has no unit to empower.");
                        return;
                    }

                    target.Attack += 2;
                    state.Log($"{target.InstanceId} gains +2 Attack from Vale.");
                });
                break;
            case "CH019" when triggerType == TriggerType.EndOfRound && !champion.TookDamageThisRound:
                yield return new("CH019", "CH019 End of Round: Hest deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "CH019",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "CH020" when triggerType == TriggerType.OnPlay && context.PlayedByPlayerId == owner.Id && context.PlayedCard?.Type == CardType.Counter:
                yield return new("CH020", "CH020 On Play: Malvek heals 1.", sink => HealChampion(state, owner.Id, 1, "Malvek, Ruin Script"));
                break;
        }
    }

    private IEnumerable<TriggerSpec> GetUnitTriggers(MatchState state, PlayerState owner, UnitState unit, TriggerType triggerType, TriggerContext context)
    {
        switch (unit.Card.Id)
        {
            case "UN003" when triggerType == TriggerType.OnPlay && context.PlayedUnit?.InstanceId == unit.InstanceId:
                yield return new("UN003", $"UN003 On Play: {unit.InstanceId} grants Defender.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id, unit.InstanceId);
                    if (target is null)
                    {
                        state.Log("Shield Acolyte has no other friendly unit.");
                        return;
                    }

                    target.HasDefender = true;
                    state.Log($"{target.InstanceId} gains Defender from {unit.InstanceId}.");
                });
                break;
            case "UN004" when triggerType == TriggerType.OnSurvive && context.DamagedUnit?.InstanceId == unit.InstanceId:
                yield return new("UN004", $"UN004 On Survive: {unit.InstanceId} gains +1 Attack.", sink =>
                {
                    unit.Attack += 1;
                    state.Log($"{unit.InstanceId} gains +1 Attack from Stoneframe Guard.");
                });
                break;
            case "UN007" when triggerType == TriggerType.OnPlay && context.PlayedUnit?.InstanceId == unit.InstanceId:
                yield return new("UN007", $"UN007 On Play: {unit.InstanceId} grants Magnet.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id, unit.InstanceId);
                    if (target is null)
                    {
                        state.Log("Static Adept has no other friendly unit.");
                        return;
                    }

                    target.HasMagnet = true;
                    state.Log($"{target.InstanceId} gains Magnet from {unit.InstanceId}.");
                });
                break;
            case "UN009" when triggerType == TriggerType.OnDeath && context.DeadUnit?.InstanceId == unit.InstanceId:
                yield return new("UN009", $"UN009 On Death: {unit.InstanceId} empowers the oldest friendly unit.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id, unit.InstanceId);
                    if (target is null)
                    {
                        state.Log("Grave Tender has no other friendly unit.");
                        return;
                    }

                    target.Attack += 1;
                    target.Health += 1;
                    state.Log($"{target.InstanceId} gains +1/+1 from Grave Tender.");
                });
                break;
            case "UN010" when triggerType == TriggerType.OnDestroyed && context.DeadUnit?.InstanceId == unit.InstanceId:
                yield return new("UN010", $"UN010 On Destroyed: {unit.InstanceId} deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "UN010",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
            case "UN011" when triggerType == TriggerType.OnDeath && context.DeadUnit?.InstanceId == unit.InstanceId:
                yield return new("UN011", $"UN011 On Death: {unit.InstanceId} heals the Champion.", sink => HealChampion(state, owner.Id, 1, "Bone Clerk"));
                break;
            case "UN012" when triggerType == TriggerType.OnDestroyed && context.DeadUnit?.InstanceId == unit.InstanceId:
                yield return new("UN012", $"UN012 On Destroyed: {unit.InstanceId} grants +1 Attack.", sink =>
                {
                    var target = SelectOldestFriendlyUnit(state, owner.Id);
                    if (target is null)
                    {
                        state.Log("Relic Rat has no friendly unit to empower.");
                        return;
                    }

                    target.Attack += 1;
                    state.Log($"{target.InstanceId} gains +1 Attack from Relic Rat.");
                });
                break;
            case "UN013" when triggerType == TriggerType.OnAttack && context.Attacker?.InstanceId == unit.InstanceId:
                yield return new("UN013", $"UN013 On Attack: {unit.InstanceId} gains +1 Attack for this attack.", sink =>
                {
                    sink?.Invoke(1);
                    state.Log($"{unit.InstanceId} gains +1 Attack for this attack from Banner Runner.");
                });
                break;
            case "UN018" when triggerType == TriggerType.OnPlay && context.PlayedUnit?.InstanceId == unit.InstanceId:
                yield return new("UN018", $"UN018 On Play: {unit.InstanceId} heals the Champion.", sink => HealChampion(state, owner.Id, 1, "Field Surgeon"));
                break;
            case "UN019" when triggerType == TriggerType.StartOfRound && owner.CounterZone.Count > 0:
                yield return new("UN019", $"UN019 Start of Round: {unit.InstanceId} gains +1 Attack.", sink =>
                {
                    unit.Attack += 1;
                    state.Log($"{unit.InstanceId} gains +1 Attack from Wardscribe Page.");
                });
                break;
            case "UN020" when triggerType == TriggerType.OnDamage && context.DamagedUnit?.InstanceId == unit.InstanceId && context.DamageCause?.IsSpellOrEffect == true:
                yield return new("UN020", $"UN020 On Damage: {unit.InstanceId} deals 1 to the enemy Champion.", sink =>
                {
                    DealDamageToChampion(state, state.GetOpponent(owner.Id), 1, new DamageCause
                    {
                        SourcePlayerId = owner.Id,
                        SourceCardId = "UN020",
                        IsSpellOrEffect = true,
                    }, ignoreDefender: true);
                });
                break;
        }
    }

    private UnitState? SelectOldestFriendlyUnit(MatchState state, string ownerId, string? excludeInstanceId = null)
    {
        return state.GetPlayer(ownerId).Battlefield
            .Where(unit => !unit.IsDead && !string.Equals(unit.InstanceId, excludeInstanceId, StringComparison.Ordinal))
            .OrderBy(unit => unit.ZoneOrder)
            .FirstOrDefault();
    }

    private UnitState? SelectTargetedEnemyUnit(MatchState state, string actingPlayerId)
    {
        var enemyUnits = state.GetOpponent(actingPlayerId).Battlefield
            .Where(unit => !unit.IsDead)
            .OrderBy(unit => unit.ZoneOrder)
            .ToList();

        var magnets = enemyUnits.Where(unit => unit.HasMagnet).ToList();
        return magnets.FirstOrDefault() ?? enemyUnits.FirstOrDefault();
    }
}
