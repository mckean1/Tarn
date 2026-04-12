namespace Tarn.Domain;

public sealed class MatchSettings
{
    public int Seed { get; init; } = 12345;
    public int MaximumRoundsPerGame { get; init; } = 200;
}

public sealed class GameEngine
{
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

    public string PlayGame(GameState state, int maximumRounds = 200)
    {
        while (state.WinnerPlayerId is null)
        {
            if (state.RoundNumber >= maximumRounds)
            {
                throw new InvalidOperationException("Exceeded round limit while simulating Tarn game.");
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

            var winnerId = PlayGame(state, settings.MaximumRoundsPerGame);
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

        var orderedBatch = batch
            .OrderBy(item => item.PlayerId == state.PriorityPlayerId ? 0 : 1)
            .ToList();

        foreach (var played in orderedBatch)
        {
            switch (played.Card)
            {
                case UnitDefinition unit:
                    ResolveUnit(state, played.PlayerId, unit);
                    break;
                case SpellDefinition spell:
                    ResolveSpell(state, played.PlayerId, spell);
                    break;
                case CounterDefinition counter:
                    ResolveCounterCard(state, played.PlayerId, counter);
                    break;
                case ChampionDefinition:
                    throw new InvalidOperationException("Champion cards do not resolve from the deck.");
            }
        }
    }

    private static void ResolveUnit(GameState state, string playerId, UnitDefinition unit)
    {
        var player = state.GetPlayer(playerId);
        var instance = new CombatCardState
        {
            InstanceId = $"{playerId}-{unit.Id}-{state.NextSequence()}",
            OwnerId = playerId,
            Definition = unit,
            EnteredRound = state.RoundNumber,
            PlayOrder = state.Sequence,
            CurrentHealth = unit.Health,
        };

        ApplyEffects(state, playerId, instance, unit.OnPlayedEffects);
        player.Board.Add(instance);
        RaiseEvent(state, TriggerEventType.UnitEnteredPlay, playerId, unit.Id, instance.InstanceId, $"Unit {unit.Id} enters play for {playerId}.");

        if (unit.Keywords.HasFlag(Keyword.Rally))
        {
            foreach (var ally in player.Board.Where(card => card.InstanceId != instance.InstanceId && card.CurrentAttack > 0))
            {
                ally.TemporaryAttackModifier += 1;
                Log(state, $"Rally grants +1 Attack this round to {ally.InstanceId}.");
            }
        }

        RaiseEvent(state, TriggerEventType.CardResolved, playerId, unit.Id, instance.InstanceId, $"Unit {unit.Id} resolves for {playerId}.");
    }

    private static void ResolveSpell(GameState state, string playerId, SpellDefinition spell)
    {
        ApplyEffects(state, playerId, null, spell.OnPlayedEffects);
        RaiseEvent(state, TriggerEventType.CardResolved, playerId, spell.Id, null, $"Spell {spell.Id} resolves for {playerId}.");
    }

    private static void ResolveCounterCard(GameState state, string playerId, CounterDefinition counter)
    {
        var player = state.GetPlayer(playerId);
        var instance = new CounterState
        {
            InstanceId = $"{playerId}-{counter.Id}-{state.NextSequence()}",
            OwnerId = playerId,
            Definition = counter,
            PlayOrder = state.Sequence,
            SetSequence = state.Sequence,
        };

        player.Counters.Add(instance);
        Log(state, $"Counter {counter.Id} is set for {playerId}.");
        RaiseEvent(state, TriggerEventType.CardResolved, playerId, counter.Id, instance.InstanceId, $"Counter {counter.Id} resolves and waits for {counter.Trigger.EventType}.");
    }

    private static void ApplyEffects(GameState state, string playerId, CombatCardState? source, IReadOnlyList<EffectDefinition> effects)
    {
        foreach (var effect in effects)
        {
            ResolveEffect(state, playerId, source, effect);
        }
    }

    private static void ResolveEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect)
    {
        switch (effect.Type)
        {
            case EffectType.Damage:
                ResolveDamageEffect(state, playerId, source, effect);
                break;
            case EffectType.Heal:
                ResolveHealEffect(state, playerId, source, effect);
                break;
            case EffectType.GrantAttackThisRound:
                ResolveGrantAttackEffect(state, playerId, source, effect);
                break;
            case EffectType.PreventAttacksThisRound:
                ResolvePreventAttackEffect(state, playerId, source, effect);
                break;
            default:
                throw new InvalidOperationException($"Unsupported effect type {effect.Type}.");
        }
    }

    private static void ResolveDamageEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect)
    {
        switch (effect.Selector)
        {
            case TargetSelector.EnemyChampion:
                ApplyDamageToChampion(state, state.GetOpponent(playerId), effect.Amount, source);
                break;
            case TargetSelector.AutoEnemyUnit:
            {
                var target = SelectAutoTargetUnit(state.GetOpponent(playerId));
                if (target is not null)
                {
                    ApplyDamageToUnit(state, target, effect.Amount, source);
                }

                break;
            }
            case TargetSelector.Source when source is not null:
                ApplyDamageToUnit(state, source, effect.Amount, source);
                break;
        }
    }

    private static void ResolveHealEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect)
    {
        if (effect.Selector == TargetSelector.Source && source is not null)
        {
            HealCombatant(state, source, effect.Amount);
        }
    }

    private static void ResolveGrantAttackEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect)
    {
        IEnumerable<CombatCardState> targets = effect.Selector switch
        {
            TargetSelector.Source when source is not null => [source],
            TargetSelector.AlliedUnitsWithAttack => state.GetPlayer(playerId).Board.Where(card => card.CurrentAttack > 0).ToList(),
            _ => [],
        };

        foreach (var target in targets)
        {
            target.TemporaryAttackModifier += effect.Amount;
            Log(state, $"{target.InstanceId} gains +{effect.Amount} Attack this round.");
        }
    }

    private static void ResolvePreventAttackEffect(GameState state, string playerId, CombatCardState? source, EffectDefinition effect)
    {
        IEnumerable<CombatCardState> targets = effect.Selector switch
        {
            TargetSelector.AutoEnemyUnit => SelectAutoTargetUnit(state.GetOpponent(playerId)) is { } unit ? [unit] : [],
            TargetSelector.Source when source is not null => [source],
            _ => [],
        };

        foreach (var target in targets)
        {
            target.PreventAttackThisRound = true;
            Log(state, $"{target.InstanceId} cannot attack this round.");
        }
    }

    private static CombatCardState? SelectAutoTargetUnit(PlayerState player)
    {
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

    private static void ApplyDamageToChampion(GameState state, PlayerState player, int amount, CombatCardState? source)
    {
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
        RaiseEvent(state, TriggerEventType.ChampionDamaged, player.Id, source?.Definition.Id, source?.InstanceId, $"Champion for {player.Id} takes {remaining} damage.");
    }

    private static void ApplyDamageToUnit(GameState state, CombatCardState target, int amount, CombatCardState? source)
    {
        target.CurrentHealth -= amount;
        Log(state, $"{target.InstanceId} takes {amount} damage.");
        RaiseEvent(state, TriggerEventType.CardResolved, target.OwnerId, source?.Definition.Id, source?.InstanceId, $"Damage resolves on {target.InstanceId}.");
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
