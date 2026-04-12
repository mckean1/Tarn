namespace Tarn.Domain;

public sealed partial class GameEngine
{
    private enum PendingEffectKind
    {
        Spell,
        Ability,
        Counter,
        Attack,
    }

    private sealed class PendingEffect
    {
        public required PendingEffectKind Kind { get; init; }
        public required string OwnerId { get; init; }
        public required string Description { get; init; }
        public required string SourceCardId { get; init; }
        public required Action Action { get; set; }
        public PendingEffect? Targets { get; set; }
        public bool IsCountered { get; set; }
        public bool PreventAttackDamage { get; set; }
        public CounterState? CounterCard { get; init; }
        public UnitState? Attacker { get; init; }
    }

    private sealed class TriggerContext
    {
        public CardDefinition? PlayedCard { get; init; }
        public string? PlayedByPlayerId { get; init; }
        public UnitState? PlayedUnit { get; init; }
        public UnitState? Attacker { get; init; }
        public UnitState? DamagedUnit { get; init; }
        public DamageCause? DamageCause { get; init; }
        public UnitState? DeadUnit { get; init; }
        public string? CounteredEffectOwnerId { get; init; }
    }

    private sealed class DamageCause
    {
        public required string SourcePlayerId { get; init; }
        public string? SourceCardId { get; init; }
        public bool IsUnitAttack { get; init; }
        public bool IsSpellOrEffect { get; init; }
        public bool IsTargetedUnitEffect { get; init; }
    }

    private sealed record TriggerSpec(string SourceCardId, string Description, Action<Action<int>?> Resolve);

    public MatchSetup CreateRandomMatchSetup(int seed)
    {
        var rng = new SeededRng(seed);
        var champions = TarnCardRegistry.Champions.Values.OrderBy(card => card.Id, StringComparer.Ordinal).ToList();

        return new MatchSetup
        {
            Seed = seed,
            PlayerOneDeck = BuildRandomDeck(champions[rng.NextInt(champions.Count)], rng),
            PlayerTwoDeck = BuildRandomDeck(champions[rng.NextInt(champions.Count)], rng),
        };
    }

    public SimulatedMatchResult RunRandomMatch(int seed) => RunMatch(CreateRandomMatchSetup(seed));

    public MatchState CreateMatchState(MatchSetup setup)
    {
        setup.PlayerOneDeck.Validate();
        setup.PlayerTwoDeck.Validate();

        var state = CreateState(setup);
        LogMatchStart(state);
        return state;
    }

    public void PlaySingleRound(MatchState state)
    {
        if (state.OvertimePending)
        {
            BeginOvertime(state);
        }

        PlayRound(state);
    }

    public SimulatedMatchResult RunMatch(MatchSetup setup)
    {
        var state = CreateMatchState(setup);

        while (state.WinnerPlayerId is null)
        {
            PlaySingleRound(state);
        }

        state.Log($"Winner: {state.WinnerPlayerId} after round {state.RoundNumber}.");

        return new SimulatedMatchResult
        {
            Seed = state.Seed,
            WinnerPlayerId = state.WinnerPlayerId,
            RoundCount = state.RoundNumber,
            PlayerOneChampionHealth = state.PlayerOne.Champion.Health,
            PlayerTwoChampionHealth = state.PlayerTwo.Champion.Health,
            ReplayLog = state.ReplayLog.ToList(),
            ReplayText = state.BuildReplayText(),
        };
    }

    private static DeckDefinition BuildRandomDeck(ChampionCardDefinition champion, SeededRng rng)
    {
        var pool = TarnCardRegistry.NonChampionPool.ToList();
        var selected = new List<CardDefinition>(30);
        var copies = new Dictionary<string, int>(StringComparer.Ordinal);

        while (selected.Count < 30)
        {
            var candidate = pool[rng.NextInt(pool.Count)];
            copies.TryGetValue(candidate.Id, out var count);
            if (count >= 3)
            {
                continue;
            }

            copies[candidate.Id] = count + 1;
            selected.Add(candidate);
        }

        return new DeckDefinition(champion, selected);
    }

    private static MatchState CreateState(MatchSetup setup)
    {
        var state = new MatchState
        {
            Setup = setup,
            Seed = setup.Seed,
            Rng = new SeededRng(setup.Seed),
            PlayerOne = CreatePlayer(setup.PlayerOneId, setup.PlayerOneDeck, setup.Seed * 2 + 1, setup.ShuffleDecks),
            PlayerTwo = CreatePlayer(setup.PlayerTwoId, setup.PlayerTwoDeck, setup.Seed * 2 + 2, setup.ShuffleDecks),
        };

        state.InitiativePlayerIndex = DetermineRoundOneInitiative(state);
        return state;
    }

    private static PlayerState CreatePlayer(string playerId, DeckDefinition deck, int deckSeed, bool shuffleDeck)
    {
        var cards = deck.MainDeck.ToList();
        if (shuffleDeck)
        {
            var rng = new SeededRng(deckSeed);
            for (var index = cards.Count - 1; index > 0; index--)
            {
                var swapIndex = rng.NextInt(index + 1);
                (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
            }
        }

        return new PlayerState
        {
            Id = playerId,
            Deck = deck,
            Champion = new ChampionState
            {
                Card = deck.Champion,
                OwnerId = playerId,
                Health = deck.Champion.Health,
                Attack = deck.Champion.Attack,
            },
            DeckCards = new Queue<CardDefinition>(cards),
        };
    }

    private static int DetermineRoundOneInitiative(MatchState state)
    {
        var first = state.PlayerOne.Champion.Card;
        var second = state.PlayerTwo.Champion.Card;
        if (first.Speed != second.Speed)
        {
            return first.Speed > second.Speed ? 0 : 1;
        }

        var order = TarnCardRegistry.ChampionSpeedTiebreakOrder;
        return order.ToList().IndexOf(first.Id) <= order.ToList().IndexOf(second.Id) ? 0 : 1;
    }

    private static void LogMatchStart(MatchState state)
    {
        state.Log($"Seed: {state.Seed}");
        state.Log($"{state.PlayerOne.Id} Champion: {FormatChampion(state.PlayerOne.Champion.Card)}");
        state.Log($"{state.PlayerTwo.Id} Champion: {FormatChampion(state.PlayerTwo.Champion.Card)}");
        state.Log($"{state.PlayerOne.Id} Deck: {string.Join(", ", state.PlayerOne.Deck.MainDeck.Select(card => card.Id))}");
        state.Log($"{state.PlayerTwo.Id} Deck: {string.Join(", ", state.PlayerTwo.Deck.MainDeck.Select(card => card.Id))}");
        state.Log($"Round 1 initiative: {state.GetPlayer(state.InitiativePlayerIndex).Id}");
    }

    private static string FormatChampion(ChampionCardDefinition champion) =>
        $"{champion.Id} {champion.Name} (Speed {champion.Speed}, Attack {champion.Attack}, Health {champion.Health})";

    private void PlayRound(MatchState state)
    {
        state.RoundNumber++;
        state.Log(string.Empty);
        state.Log($"=== Round {state.RoundNumber} ===");

        foreach (var player in state.Players)
        {
            player.Champion.TookDamageThisRound = false;
            player.Champion.EnemyChampionTookDamageThisRound = false;
        }

        ResolveTriggers(state, TriggerType.StartOfRound, new TriggerContext());
        if (ShouldStopRound(state))
        {
            return;
        }

        var initiativePlayer = state.GetPlayer(state.InitiativePlayerIndex);
        var otherPlayer = state.GetPlayer(1 - state.InitiativePlayerIndex);

        AutoPlayTopCard(state, initiativePlayer);
        if (ShouldStopRound(state))
        {
            return;
        }

        AutoPlayTopCard(state, otherPlayer);
        if (ShouldStopRound(state))
        {
            return;
        }

        ResolveAttackStep(state);
        if (ShouldStopRound(state))
        {
            return;
        }

        ResolveTriggers(state, TriggerType.EndOfRound, new TriggerContext());
        if (ShouldStopRound(state))
        {
            return;
        }

        state.InitiativePlayerIndex = 1 - state.InitiativePlayerIndex;
        state.Log($"Initiative passes to {state.GetPlayer(state.InitiativePlayerIndex).Id}.");
    }

    private static bool ShouldStopRound(MatchState state) => state.WinnerPlayerId is not null || state.OvertimePending;

    private void AutoPlayTopCard(MatchState state, PlayerState player)
    {
        if (player.DeckCards.Count == 0)
        {
            ApplyFatigue(state, player);
            return;
        }

        var card = player.DeckCards.Dequeue();
        state.Log($"{player.Id} plays {card.Id} {card.Name}.");

        switch (card)
        {
            case UnitCardDefinition unitCard:
            {
                var unit = SummonUnit(state, player, unitCard);
                ResolveTriggers(state, TriggerType.OnPlay, new TriggerContext
                {
                    PlayedCard = card,
                    PlayedByPlayerId = player.Id,
                    PlayedUnit = unit,
                });
                PerformDeathCheck(state);
                CheckWinCondition(state);
                break;
            }
            case SpellCardDefinition spellCard:
            {
                ResolveTriggers(state, TriggerType.OnPlay, new TriggerContext
                {
                    PlayedCard = card,
                    PlayedByPlayerId = player.Id,
                });

                ResolvePendingEffect(state, new PendingEffect
                {
                    Kind = PendingEffectKind.Spell,
                    OwnerId = player.Id,
                    Description = $"{player.Id}'s spell {card.Id} {card.Name}",
                    SourceCardId = card.Id,
                    Action = () => ResolveSpell(state, player, spellCard),
                });
                break;
            }
            case CounterCardDefinition counterCard:
            {
                SetCounter(state, player, counterCard);
                ResolveTriggers(state, TriggerType.OnPlay, new TriggerContext
                {
                    PlayedCard = card,
                    PlayedByPlayerId = player.Id,
                });
                PerformDeathCheck(state);
                CheckWinCondition(state);
                break;
            }
        }
    }

    private UnitState SummonUnit(MatchState state, PlayerState owner, UnitCardDefinition card)
    {
        var unit = new UnitState
        {
            InstanceId = $"{owner.Id}-{card.Id}-{state.NextZoneOrder}",
            OwnerId = owner.Id,
            Card = card,
            EnteredRound = state.RoundNumber,
            ZoneOrder = state.NextZoneOrder++,
            Attack = card.Attack,
            Health = card.Health,
            HasDefender = card.HasDefender,
            HasMagnet = card.HasMagnet,
        };

        owner.Battlefield.Add(unit);
        state.Log($"{unit.InstanceId} enters the Battlefield as {card.Attack}/{card.Health}.");
        return unit;
    }

    private void SetCounter(MatchState state, PlayerState owner, CounterCardDefinition card)
    {
        var counter = new CounterState
        {
            InstanceId = $"{owner.Id}-{card.Id}-{state.NextZoneOrder}",
            OwnerId = owner.Id,
            Card = card,
            ZoneOrder = state.NextZoneOrder++,
        };

        owner.CounterZone.Add(counter);
        state.Log($"{counter.InstanceId} enters the Counter Zone.");
    }
}
