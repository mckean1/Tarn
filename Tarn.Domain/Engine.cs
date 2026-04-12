namespace Tarn.Domain;

public sealed class MatchSettings
{
    public int Seed { get; init; } = 12345;
    public int? DebugRoundLimit { get; init; }
}

public sealed class GameEngine
{
    private sealed record CombatCardSnapshot(
        string InstanceId,
        long PlayOrder,
        Keyword Keywords,
        int CurrentHealth,
        int CurrentAttack,
        bool MarkedForDestruction);

    private sealed class DeferredEvent
    {
        public required TriggerEventType EventType { get; init; }
        public required string SourcePlayerId { get; init; }
        public string? SourceCardId { get; init; }
        public string? SourceInstanceId { get; init; }
        public required string Message { get; init; }
    }

    private sealed class ResolutionBatchContext
    {
        public required IReadOnlyDictionary<string, IReadOnlyList<CombatCardSnapshot>> SnapshotBoards { get; init; }
        public List<CombatCardState> PendingUnits { get; } = [];
        public List<CounterState> PendingCounters { get; } = [];
        public List<DeferredEvent> DeferredEvents { get; } = [];
        public Dictionary<string, int> PendingDamageByInstanceId { get; } = new(StringComparer.Ordinal);
    }

    public GameState CreateGame(
        string higherSeedPlayerId,
        string playerOneId,
        DeckDefinition playerOneDeck,
        string playerTwoId,
        DeckDefinition playerTwoDeck,
        int seed,
        int gameNumber)
    {
        playerOneDeck.Validate();
        playerTwoDeck.Validate();

        var priorityPlayerId = DeterminePriorityPlayerId(gameNumber, higherSeedPlayerId, playerOneId, playerTwoId);

        var playerOne = CreatePlayer(playerOneId, playerOneDeck, seed + gameNumber);
        var playerTwo = CreatePlayer(playerTwoId, playerTwoDeck, seed + gameNumber + 77);

        return new GameState
        {
            PlayerOne = playerOne,
            PlayerTwo = playerTwo,
            HigherSeedPlayerId = higherSeedPlayerId,
            PriorityPlayerId = priorityPlayerId,
            Seed = seed,
            GameNumber = gameNumber,
            RoundNumber = 0,
            CurrentStep = RoundStep.None,
        };
    }

    public string PlayGame(GameState state, int? debugRoundLimit = null)
    {
        while (state.WinnerPlayerId is null)
        {
            if (debugRoundLimit is int limit && state.RoundNumber >= limit)
            {
                throw new InvalidOperationException("Debug safeguard exceeded round limit while simulating Tarn game.");
            }

            PlayRound(state);
        }

        return state.WinnerPlayerId;
    }

    public MatchResult PlayMatch(
        string higherSeedPlayerId,
        string playerOneId,
        DeckDefinition playerOneDeck,
        string playerTwoId,
        DeckDefinition playerTwoDeck,
        MatchSettings? settings = null)
    {
        settings ??= new MatchSettings();
        var gameResults = new List<GameResult>();
        var playerOneWins = 0;
        var playerTwoWins = 0;
        var gameNumber = 1;

        while (playerOneWins < 2 && playerTwoWins < 2)
        {
            var state = CreateGame(
                higherSeedPlayerId,
                playerOneId,
                playerOneDeck,
                playerTwoId,
                playerTwoDeck,
                settings.Seed,
                gameNumber);

            var winnerId = PlayGame(state, settings.DebugRoundLimit);
            if (winnerId == playerOneId)
            {
                playerOneWins++;
            }
            else
            {
                playerTwoWins++;
            }

            gameResults.Add(new GameResult(1, 0, winnerId, state.GetOpponent(winnerId).Id));
            gameNumber++;
        }

        var winnerPlayerId = playerOneWins > playerTwoWins ? playerOneId : playerTwoId;
        var loserPlayerId = winnerPlayerId == playerOneId ? playerTwoId : playerOneId;

        return new MatchResult
        {
            WinnerPlayerId = winnerPlayerId,
            LoserPlayerId = loserPlayerId,
            WinnerGameWins = Math.Max(playerOneWins, playerTwoWins),
            LoserGameWins = Math.Min(playerOneWins, playerTwoWins),
            Games = gameResults,
        };
    }

    public void PlayRound(GameState state)
    {
        state.RoundNumber++;
        state.DestroyedThisRound.Clear();
        ResetRoundModifiers(state);

        Step(state, RoundStep.Play, () =>
        {
            var played = new List<PlayedCardState>();
            foreach (var player in state.Players)
            {
                var card = DrawTopCard(player);
                if (card is null)
                {
                    ApplyFatigue(state, player);
                    continue;
                }

                played.Add(new PlayedCardState { PlayerId = player.Id, Card = card });
                RaiseEvent(state, TriggerEventType.CardPlayed, player.Id, card.Id, null, $"Player {player.Id} plays {card.Id}.");
            }

            var quick = played.Where(item => item.Card.Keywords.HasFlag(Keyword.Quick)).ToList();
            var normal = played.Where(item => !item.Card.Keywords.HasFlag(Keyword.Quick)).ToList();

            if (quick.Count > 0)
            {
                Step(state, RoundStep.Quick, () => ResolvePlayedBatch(state, quick));
            }

            Step(state, RoundStep.Resolution, () => ResolvePlayedBatch(state, normal));
        });

        Step(state, RoundStep.Attack, () => ResolveAttackStep(state));
        Step(state, RoundStep.End, () => ResolveEndStep(state));
        Step(state, RoundStep.Destruction, () => ResolveDestructionStep(state));
        Step(state, RoundStep.LastWish, () => ResolveLastWishStep(state));
        Step(state, RoundStep.WinCheck, () => ResolveWinCheck(state));
    }

    private static void Step(GameState state, RoundStep step, Action action)
    {
        state.CurrentStep = step;
        action();
    }

    private static PlayerState CreatePlayer(string playerId, DeckDefinition deck, int seed)
    {
        var champion = new CombatCardState
        {
            InstanceId = $"{playerId}-champion",
            OwnerId = playerId,
            Definition = deck.Champion,
            EnteredRound = 0,
            PlayOrder = 0,
            CurrentHealth = deck.Champion.Health,
        };

        var random = new Random(seed);
        return new PlayerState
        {
            Id = playerId,
            Deck = deck,
            Champion = champion,
            Library = new Queue<CardDefinition>(deck.MainDeck.OrderBy(_ => random.Next())),
        };
    }

    private static string DeterminePriorityPlayerId(
        int gameNumber,
        string higherSeedPlayerId,
        string playerOneId,
        string playerTwoId)
    {
        var higherSeedIsPlayerOne = string.Equals(higherSeedPlayerId, playerOneId, StringComparison.Ordinal);
        if (gameNumber % 2 == 1)
        {
            return higherSeedPlayerId;
        }

        return higherSeedIsPlayerOne ? playerTwoId : playerOneId;
    }

    private static CardDefinition? DrawTopCard(PlayerState player) =>
        player.Library.Count == 0 ? null : player.Library.Dequeue();

    private static void ResetRoundModifiers(GameState state)
    {
        foreach (var combatant in state.Players.SelectMany(player => player.Board).Append(state.PlayerOne.Champion).Append(state.PlayerTwo.Champion))
        {
            combatant.TemporaryAttackModifier = 0;
            combatant.PreventAttackThisRound = false;
            combatant.DestroyedThisRound = false;
        }
    }

    private static void ResolvePlayedBatch(GameState state, IReadOnlyList<PlayedCardState> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var context = CreateResolutionBatchContext(state);
        var orderedBatch = batch
            .OrderBy(item => item.PlayerId == state.PriorityPlayerId ? 0 : 1)
            .ToList();

        foreach (var played in orderedBatch)
        {
            switch (played.Card)
            {
                case UnitDefinition unit:
                    ResolveUnit(state, played.PlayerId, unit, context);
                    break;
                case SpellDefinition spell:
                    ResolveSpell(state, played.PlayerId, spell, context);
                    break;
                case CounterDefinition counter:
                    ResolveCounterCard(state, played.PlayerId, counter, context);
                    break;
                case ChampionDefinition:
                    throw new InvalidOperationException("Champion cards do not resolve from the deck.");
            }
        }

        CommitSimultaneousBatch(state, context);
    }

    private static ResolutionBatchContext CreateResolutionBatchContext(GameState state)
    {
        var snapshots = state.Players.ToDictionary(
            player => player.Id,
            player => (IReadOnlyList<CombatCardSnapshot>)player.Board
                .Select(unit => new CombatCardSnapshot(
                    unit.InstanceId,
                    unit.PlayOrder,
                    unit.Keywords,
                    unit.CurrentHealth,
                    unit.CurrentAttack,
                    unit.MarkedForDestruction))
                .OrderBy(unit => unit.PlayOrder)
                .ToList(),
            StringComparer.Ordinal);

        return new ResolutionBatchContext
        {
            SnapshotBoards = snapshots,
        };
    }

    private static void CommitSimultaneousBatch(GameState state, ResolutionBatchContext context)
    {
        ApplyPendingBatchDamage(state, context);

        foreach (var unit in context.PendingUnits.OrderBy(unit => unit.PlayOrder))
        {
            state.GetPlayer(unit.OwnerId).Board.Add(unit);
        }

        FlushDeferredEvents(state, context.DeferredEvents);

        foreach (var counter in context.PendingCounters.OrderBy(counter => counter.PlayOrder))
        {
            counter.SetSequence = state.Sequence;
            state.GetPlayer(counter.OwnerId).Counters.Add(counter);
            Log(state, $"Counter {counter.Definition.Id} is set for {counter.OwnerId}.");
        }
    }

    private static void ApplyPendingBatchDamage(GameState state, ResolutionBatchContext context)
    {
        foreach (var pendingDamage in context.PendingDamageByInstanceId)
        {
            var target = TryGetCombatantByInstanceId(state, context, pendingDamage.Key);
            if (target is null)
            {
                continue;
            }

            target.CurrentHealth -= pendingDamage.Value;
        }
    }

    private static CombatCardState? TryGetCombatantByInstanceId(GameState state, ResolutionBatchContext context, string instanceId)
    {
        foreach (var player in state.Players)
        {
            if (player.Champion.InstanceId == instanceId)
            {
                return player.Champion;
            }

            var boardUnit = player.Board.FirstOrDefault(unit => unit.InstanceId == instanceId);
            if (boardUnit is not null)
            {
                return boardUnit;
            }
        }

        return context.PendingUnits.FirstOrDefault(unit => unit.InstanceId == instanceId);
    }

    private static void FlushDeferredEvents(GameState state, IReadOnlyList<DeferredEvent> deferredEvents)
    {
        foreach (var deferredEvent in deferredEvents)
        {
            RaiseEvent(
                state,
                deferredEvent.EventType,
                deferredEvent.SourcePlayerId,
                deferredEvent.SourceCardId,
                deferredEvent.SourceInstanceId,
                deferredEvent.Message);
        }
    }

    private static void ResolveUnit(GameState state, string playerId, UnitDefinition unit, ResolutionBatchContext? context = null)
    {
        var instance = new CombatCardState
        {
            InstanceId = $"{playerId}-{unit.Id}-{state.NextSequence()}",
            OwnerId = playerId,
            Definition = unit,
            EnteredRound = state.RoundNumber,
            PlayOrder = state.Sequence,
            CurrentHealth = unit.Health,
        };

        ApplyEffects(state, playerId, instance, unit.OnPlayedEffects, context);

        if (unit.Keywords.HasFlag(Keyword.Rally))
        {
            foreach (var ally in GetAlliedUnitsWithAttack(state, playerId, context))
            {
                ally.TemporaryAttackModifier += 1;
                Log(state, $"Rally grants +1 Attack this round to {ally.InstanceId}.");
            }
        }

        if (context is null)
        {
            state.GetPlayer(playerId).Board.Add(instance);
            RaiseEvent(state, TriggerEventType.UnitEnteredPlay, playerId, unit.Id, instance.InstanceId, $"Unit {unit.Id} enters play for {playerId}.");
            RaiseEvent(state, TriggerEventType.CardResolved, playerId, unit.Id, instance.InstanceId, $"Unit {unit.Id} resolves for {playerId}.");
            return;
        }

        context.PendingUnits.Add(instance);
        context.DeferredEvents.Add(new DeferredEvent
        {
            EventType = TriggerEventType.UnitEnteredPlay,
            SourcePlayerId = playerId,
            SourceCardId = unit.Id,
            SourceInstanceId = instance.InstanceId,
            Message = $"Unit {unit.Id} enters play for {playerId}.",
        });
        context.DeferredEvents.Add(new DeferredEvent
        {
            EventType = TriggerEventType.CardResolved,
            SourcePlayerId = playerId,
            SourceCardId = unit.Id,
            SourceInstanceId = instance.InstanceId,
            Message = $"Unit {unit.Id} resolves for {playerId}.",
        });
    }

    private static void ResolveSpell(GameState state, string playerId, SpellDefinition spell, ResolutionBatchContext? context = null)
    {
        ApplyEffects(state, playerId, null, spell.OnPlayedEffects, context);

        if (context is null)
        {
            RaiseEvent(state, TriggerEventType.CardResolved, playerId, spell.Id, null, $"Spell {spell.Id} resolves for {playerId}.");
            return;
        }

        context.DeferredEvents.Add(new DeferredEvent
        {
            EventType = TriggerEventType.CardResolved,
            SourcePlayerId = playerId,
            SourceCardId = spell.Id,
            SourceInstanceId = null,
            Message = $"Spell {spell.Id} resolves for {playerId}.",
        });
    }

    private static void ResolveCounterCard(GameState state, string playerId, CounterDefinition counter, ResolutionBatchContext? context = null)
    {
        var instance = new CounterState
        {
            InstanceId = $"{playerId}-{counter.Id}-{state.NextSequence()}",
            OwnerId = playerId,
            Definition = counter,
            PlayOrder = state.Sequence,
            SetSequence = 0,
        };

        if (context is null)
        {
            instance.SetSequence = state.Sequence;
            state.GetPlayer(playerId).Counters.Add(instance);
            Log(state, $"Counter {counter.Id} is set for {playerId}.");
            return;
        }

        context.PendingCounters.Add(instance);
    }

    private static void ApplyEffects(GameState state, string playerId, CombatCardState? source, IReadOnlyList<EffectDefinition> effects, ResolutionBatchContext? context = null)
    {
        foreach (var effect in effects)
        {
            ResolveEffect(state, playerId, source, effect, context);
        }
    }

    private static void ResolveEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect, ResolutionBatchContext? context = null)
    {
        switch (effect.Type)
        {
            case EffectType.Damage:
                ResolveDamageEffect(state, playerId, source, effect, context);
                break;
            case EffectType.Heal:
                ResolveHealEffect(state, playerId, source, effect, context);
                break;
            case EffectType.GrantAttackThisRound:
                ResolveGrantAttackEffect(state, playerId, source, effect, context);
                break;
            case EffectType.PreventAttacksThisRound:
                ResolvePreventAttackEffect(state, playerId, source, effect, context);
                break;
            default:
                throw new InvalidOperationException($"Unsupported effect type {effect.Type}.");
        }
    }

    private static void ResolveDamageEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect, ResolutionBatchContext? context = null)
    {
        switch (effect.Selector)
        {
            case TargetSelector.EnemyChampion:
                ApplyDamageToChampion(state, state.GetOpponent(playerId), effect.Amount, source, context);
                break;
            case TargetSelector.AutoEnemyUnit:
            {
                var target = SelectAutoTargetUnit(state, state.GetOpponent(playerId), context);
                if (target is not null)
                {
                    ApplyDamageToUnit(state, target, effect.Amount, source, context);
                }

                break;
            }
            case TargetSelector.Source when source is not null:
                ApplyDamageToUnit(state, source, effect.Amount, source, context);
                break;
        }
    }

    private static void ResolveHealEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect, ResolutionBatchContext? context = null)
    {
        if (effect.Selector == TargetSelector.Source && source is not null)
        {
            HealCombatant(state, source, effect.Amount);
        }
    }

    private static void ResolveGrantAttackEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect, ResolutionBatchContext? context = null)
    {
        IEnumerable<CombatCardState> targets = effect.Selector switch
        {
            TargetSelector.Source when source is not null => [source],
            TargetSelector.AlliedUnitsWithAttack => GetAlliedUnitsWithAttack(state, playerId, context),
            _ => [],
        };

        foreach (var target in targets)
        {
            target.TemporaryAttackModifier += effect.Amount;
            Log(state, $"{target.InstanceId} gains +{effect.Amount} Attack this round.");
        }
    }

    private static void ResolvePreventAttackEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect, ResolutionBatchContext? context = null)
    {
        IEnumerable<CombatCardState> targets = effect.Selector switch
        {
            TargetSelector.AutoEnemyUnit => SelectAutoTargetUnit(state, state.GetOpponent(playerId), context) is { } unit ? [unit] : [],
            TargetSelector.Source when source is not null => [source],
            _ => [],
        };

        foreach (var target in targets)
        {
            target.PreventAttackThisRound = true;
            Log(state, $"{target.InstanceId} cannot attack this round.");
        }
    }

    private static IEnumerable<CombatCardState> GetAlliedUnitsWithAttack(GameState state, string playerId, ResolutionBatchContext? context)
    {
        if (context is null)
        {
            return state.GetPlayer(playerId).Board.Where(card => card.CurrentAttack > 0).OrderBy(card => card.PlayOrder).ToList();
        }

        var player = state.GetPlayer(playerId);
        return context.SnapshotBoards[playerId]
            .Where(card => card.CurrentAttack > 0 && !card.MarkedForDestruction)
            .Select(snapshot => player.Board.First(card => card.InstanceId == snapshot.InstanceId))
            .ToList();
    }

    private static CombatCardState? SelectAutoTargetUnit(GameState state, PlayerState player, ResolutionBatchContext? context = null)
    {
        if (context is not null)
        {
            return SelectAutoTargetUnitFromSnapshot(state, player, context);
        }

        var tauntUnits = player.Board
            .Where(unit => !unit.MarkedForDestruction && unit.Keywords.HasFlag(Keyword.Taunt))
            .OrderBy(unit => unit.PlayOrder)
            .ToList();

        if (tauntUnits.Count > 0)
        {
            return tauntUnits[0];
        }

        return player.Board
            .Where(unit => !unit.MarkedForDestruction)
            .OrderBy(unit => unit.PlayOrder)
            .FirstOrDefault();
    }

    private static CombatCardState? SelectAutoTargetUnitFromSnapshot(GameState state, PlayerState player, ResolutionBatchContext context)
    {
        var snapshotUnits = context.SnapshotBoards[player.Id];
        var tauntTarget = snapshotUnits
            .Where(unit => !unit.MarkedForDestruction && unit.Keywords.HasFlag(Keyword.Taunt))
            .OrderBy(unit => unit.PlayOrder)
            .FirstOrDefault();

        var targetSnapshot = tauntTarget
            ?? snapshotUnits
                .Where(unit => !unit.MarkedForDestruction)
                .OrderBy(unit => unit.PlayOrder)
                .FirstOrDefault();

        if (targetSnapshot is null)
        {
            return null;
        }

        return player.Board.FirstOrDefault(unit => unit.InstanceId == targetSnapshot.InstanceId);
    }

    private static void ResolveAttackStep(GameState state)
    {
        var attacks = new List<(string AttackerId, string DefenderPlayerId, int Damage)>();

        foreach (var player in state.Players)
        {
            var opponent = state.GetOpponent(player.Id);
            foreach (var unit in player.Board
                         .Where(unit => unit.CurrentAttack > 0
                                        && unit.EnteredRound < state.RoundNumber
                                        && !unit.MarkedForDestruction
                                        && !unit.PreventAttackThisRound)
                         .OrderBy(unit => unit.PlayOrder))
            {
                attacks.Add((unit.InstanceId, opponent.Id, unit.CurrentAttack));
            }

            if (player.Champion.CurrentAttack > 0 && !player.Champion.PreventAttackThisRound)
            {
                attacks.Add((player.Champion.InstanceId, opponent.Id, player.Champion.CurrentAttack));
            }
        }

        foreach (var attack in attacks)
        {
            ApplyDamageToChampion(state, state.GetPlayer(attack.DefenderPlayerId), attack.Damage, null);
            Log(state, $"{attack.AttackerId} attacks for {attack.Damage} damage.");
        }
    }

    private static void ResolveEndStep(GameState state)
    {
        foreach (var combatant in state.Players
                     .SelectMany(player => player.Board.Cast<CombatCardState>().Append(player.Champion))
                     .Where(card => card.Keywords.HasFlag(Keyword.Regen)))
        {
            HealCombatant(state, combatant, 1);
        }

        RaiseEvent(state, TriggerEventType.RoundEnded, state.PriorityPlayerId, null, null, $"Round {state.RoundNumber} ends.");
    }

    private static void ResolveDestructionStep(GameState state)
    {
        foreach (var player in state.Players)
        {
            var destroyed = player.Board
                .Where(unit => unit.CurrentHealth <= 0 || unit.MarkedForDestruction)
                .OrderBy(unit => unit.PlayOrder)
                .ToList();

            foreach (var unit in destroyed)
            {
                unit.DestroyedThisRound = true;
                player.Board.Remove(unit);
                state.DestroyedThisRound.Add(unit);
                RaiseEvent(state, TriggerEventType.UnitDestroyed, player.Id, unit.Definition.Id, unit.InstanceId, $"Unit {unit.InstanceId} is destroyed.");
            }
        }
    }

    private static void ResolveLastWishStep(GameState state)
    {
        foreach (var destroyed in state.DestroyedThisRound.OrderBy(unit => unit.PlayOrder).ToList())
        {
            if (!destroyed.Keywords.HasFlag(Keyword.LastWish))
            {
                continue;
            }

            ApplyEffects(state, destroyed.OwnerId, destroyed, destroyed.Definition.LastWishEffects);
            Log(state, $"Last Wish resolves for {destroyed.InstanceId}.");
        }
    }

    private static void ResolveWinCheck(GameState state)
    {
        var playerOneHealth = state.PlayerOne.Champion.CurrentHealth;
        var playerTwoHealth = state.PlayerTwo.Champion.CurrentHealth;
        var playerOneDefeated = playerOneHealth <= 0;
        var playerTwoDefeated = playerTwoHealth <= 0;

        if (!playerOneDefeated && !playerTwoDefeated)
        {
            return;
        }

        if (playerOneDefeated ^ playerTwoDefeated)
        {
            state.WinnerPlayerId = playerOneDefeated ? state.PlayerTwo.Id : state.PlayerOne.Id;
            Log(state, $"Winner determined: {state.WinnerPlayerId}.");
            return;
        }

        if (playerOneHealth != playerTwoHealth)
        {
            state.WinnerPlayerId = playerOneHealth > playerTwoHealth ? state.PlayerOne.Id : state.PlayerTwo.Id;
            Log(state, $"Winner determined by remaining Champion health: {state.WinnerPlayerId}.");
            return;
        }

        ResetForOvertime(state);
    }

    private static void ResetForOvertime(GameState state)
    {
        state.OvertimeCount++;
        state.RoundNumber = 0;
        state.PlayerOne.FatigueCount = 0;
        state.PlayerTwo.FatigueCount = 0;
        state.PlayerOne.Board.Clear();
        state.PlayerTwo.Board.Clear();
        state.PlayerOne.Counters.Clear();
        state.PlayerTwo.Counters.Clear();
        state.PlayerOne.Champion.CurrentHealth = state.PlayerOne.Champion.PrintedHealth;
        state.PlayerTwo.Champion.CurrentHealth = state.PlayerTwo.Champion.PrintedHealth;

        var oneRandom = new Random(state.Seed + state.GameNumber + state.OvertimeCount);
        var twoRandom = new Random(state.Seed + state.GameNumber + state.OvertimeCount + 5000);
        state.PlayerOne.Library = new Queue<CardDefinition>(state.PlayerOne.Deck.MainDeck.OrderBy(_ => oneRandom.Next()));
        state.PlayerTwo.Library = new Queue<CardDefinition>(state.PlayerTwo.Deck.MainDeck.OrderBy(_ => twoRandom.Next()));

        Log(state, $"Overtime {state.OvertimeCount} begins as a fresh game.");
    }

    private static void ApplyFatigue(GameState state, PlayerState player)
    {
        player.FatigueCount++;
        ApplyDamageToChampion(state, player, player.FatigueCount, null);
        Log(state, $"{player.Id} takes {player.FatigueCount} fatigue damage.");
    }

    private static void ApplyDamageToChampion(GameState state, PlayerState player, int amount, CombatCardState? source, ResolutionBatchContext? context = null)
    {
        if (context is not null)
        {
            ApplyDamageToChampionUsingSnapshot(state, player, amount, source, context);
            return;
        }

        var remaining = amount;
        foreach (var defender in player.Board
                     .Where(unit => unit.Keywords.HasFlag(Keyword.Defender) && !unit.MarkedForDestruction)
                     .OrderBy(unit => unit.PlayOrder))
        {
            if (remaining <= 0)
            {
                break;
            }

            defender.CurrentHealth -= remaining;
            Log(state, $"{remaining} damage is redirected to Defender {defender.InstanceId}.");
            remaining = Math.Max(0, -defender.CurrentHealth);
        }

        if (remaining <= 0)
        {
            return;
        }

        player.Champion.CurrentHealth -= remaining;
        if (context is null)
        {
            RaiseEvent(state, TriggerEventType.ChampionDamaged, player.Id, source?.Definition.Id, source?.InstanceId, $"Champion for {player.Id} takes {remaining} damage.");
            return;
        }

        context.DeferredEvents.Add(new DeferredEvent
        {
            EventType = TriggerEventType.ChampionDamaged,
            SourcePlayerId = player.Id,
            SourceCardId = source?.Definition.Id,
            SourceInstanceId = source?.InstanceId,
            Message = $"Champion for {player.Id} takes {remaining} damage.",
        });
    }

    private static void ApplyDamageToChampionUsingSnapshot(
        GameState state,
        PlayerState player,
        int amount,
        CombatCardState? source,
        ResolutionBatchContext context)
    {
        var remaining = amount;
        foreach (var defender in context.SnapshotBoards[player.Id]
                     .Where(unit => unit.Keywords.HasFlag(Keyword.Defender) && !unit.MarkedForDestruction)
                     .OrderBy(unit => unit.PlayOrder))
        {
            if (remaining <= 0)
            {
                break;
            }

            var intercepted = Math.Min(remaining, Math.Max(0, defender.CurrentHealth));
            if (intercepted <= 0)
            {
                continue;
            }

            AddPendingDamage(context, defender.InstanceId, intercepted);
            Log(state, $"{intercepted} damage is redirected to Defender {defender.InstanceId}.");
            remaining -= intercepted;
        }

        if (remaining <= 0)
        {
            return;
        }

        AddPendingDamage(context, player.Champion.InstanceId, remaining);
        context.DeferredEvents.Add(new DeferredEvent
        {
            EventType = TriggerEventType.ChampionDamaged,
            SourcePlayerId = player.Id,
            SourceCardId = source?.Definition.Id,
            SourceInstanceId = source?.InstanceId,
            Message = $"Champion for {player.Id} takes {remaining} damage.",
        });
    }

    private static void AddPendingDamage(ResolutionBatchContext context, string instanceId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        context.PendingDamageByInstanceId.TryGetValue(instanceId, out var currentAmount);
        context.PendingDamageByInstanceId[instanceId] = currentAmount + amount;
    }

    private static void ApplyDamageToUnit(GameState state, CombatCardState target, int amount, CombatCardState? source, ResolutionBatchContext? context = null)
    {
        target.CurrentHealth -= amount;
        Log(state, $"{target.InstanceId} takes {amount} damage.");
        if (context is null)
        {
            RaiseEvent(state, TriggerEventType.CardResolved, target.OwnerId, source?.Definition.Id, source?.InstanceId, $"Damage resolves on {target.InstanceId}.");
            return;
        }

        context.DeferredEvents.Add(new DeferredEvent
        {
            EventType = TriggerEventType.CardResolved,
            SourcePlayerId = target.OwnerId,
            SourceCardId = source?.Definition.Id,
            SourceInstanceId = source?.InstanceId,
            Message = $"Damage resolves on {target.InstanceId}.",
        });
    }

    private static void HealCombatant(GameState state, CombatCardState target, int amount)
    {
        var maxHealth = target.PrintedHealth;
        var actual = Math.Max(0, Math.Min(amount, maxHealth - target.CurrentHealth));
        if (actual == 0)
        {
            return;
        }

        target.CurrentHealth += actual;
        Log(state, $"{target.InstanceId} heals {actual}.");
    }

    private static void RaiseEvent(
        GameState state,
        TriggerEventType eventType,
        string sourcePlayerId,
        string? sourceCardId,
        string? sourceInstanceId,
        string message)
    {
        var replay = new ReplayEvent
        {
            Sequence = state.NextSequence(),
            EventType = eventType,
            SourcePlayerId = sourcePlayerId,
            SourceCardId = sourceCardId,
            SourceInstanceId = sourceInstanceId,
        };

        state.Replay.Add(replay);
        Log(state, message);
        ResolveTriggeredCounters(state, replay);
    }

    private static void ResolveTriggeredCounters(GameState state, ReplayEvent replay)
    {
        var counters = state.Players
            .SelectMany(player => player.Counters)
            .Where(counter =>
                counter.Definition.Trigger.EventType == replay.EventType &&
                counter.SetSequence < replay.Sequence &&
                (!string.Equals(counter.InstanceId, replay.SourceInstanceId, StringComparison.Ordinal) ||
                 counter.Definition.Trigger.AllowSelfTrigger))
            .OrderBy(counter => counter.PlayOrder)
            .ToList();

        foreach (var counter in counters)
        {
            var owner = state.GetPlayer(counter.OwnerId);
            owner.Counters.Remove(counter);
            Log(state, $"Counter {counter.Definition.Id} triggers for {counter.OwnerId}.");

            foreach (var effect in counter.Definition.OnPlayedEffects)
            {
                ResolveEffect(state, counter.OwnerId, null, effect);
            }
        }
    }

    private static void Log(GameState state, string message)
    {
        state.Logs.Add(new GameLogEntry
        {
            Sequence = state.NextSequence(),
            Round = state.RoundNumber,
            Step = state.CurrentStep,
            Message = message,
        });
    }
}
