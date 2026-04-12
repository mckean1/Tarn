using Regex = System.Text.RegularExpressions.Regex;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class MatchReplayQueries
{
    private static readonly Regex CardIdPattern = new(@"\b(?:CH|CT|SP|UN)\d{3}\b");
    private static readonly Regex InstanceIdPattern = new(@"\b[\w-]+-[A-Z]{2}\d{3}-\d+\b");
    private static readonly Lazy<World> PreviewWorld = new(static () => new WorldFactory().CreateNewWorld(1, "Replay"));

    private readonly GameEngine engine = new();
    private readonly WorldSimulator simulator = new();

    public MatchReplayViewModel? Build(World world, string matchId)
    {
        var fixture = world.Season.Schedule.FirstOrDefault(match => match.Id == matchId);
        if (fixture?.Result is null)
        {
            return null;
        }

        var homePlayer = world.Players[fixture.HomePlayerId];
        var awayPlayer = world.Players[fixture.AwayPlayerId];
        var setup = BuildSetup(world, fixture);
        var state = engine.CreateMatchState(setup);
        var snapshots = new List<RoundSnapshotViewModel> { CaptureSnapshot(world, fixture, state, 0) };
        while (state.WinnerPlayerId is null)
        {
            engine.PlaySingleRound(state);
            snapshots.Add(CaptureSnapshot(world, fixture, state, state.ReplayLog.Count));
        }

        var phraseContext = new ReplayPhraseContext(
            fixture.HomePlayerId,
            fixture.AwayPlayerId,
            homePlayer.Name,
            awayPlayer.Name,
            cardId => TryResolveCardName(world, cardId));
        var title = $"{homePlayer.Name} vs {awayPlayer.Name}";
        var result = $"{world.Players[fixture.Result.WinnerPlayerId].Name} wins {fixture.Result.WinnerGameWins}-{fixture.Result.LoserGameWins}";
        return new MatchReplayViewModel(
            fixture.Id,
            title,
            world.Players[ResolveInitialPlayerId(fixture)].Name,
            result,
            BuildReplayInfoLines(state),
            state.ReplayLog.Select(line => PhraseEvent(line, phraseContext)).ToList(),
            snapshots);
    }

    public static string PhraseEvent(string raw)
        => PhraseEvent(raw, new ReplayPhraseContext("P1", "P2", "Home", "Away", TryResolvePreviewCardName));

    private static string PhraseEvent(string raw, ReplayPhraseContext context)
    {
        var text = raw;
        var pipeIndex = text.IndexOf('|');
        if (pipeIndex >= 0)
        {
            text = text[(pipeIndex + 1)..].Trim();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (text.StartsWith("Seed:", StringComparison.Ordinal))
        {
            return $"Replay seed {text[5..].Trim()}.";
        }

        if (TryPhraseChampionLine(text, context, out var championLine)
            || TryPhraseDeckLine(text, context, out championLine)
            || TryPhraseRoundStart(text, out championLine)
            || TryPhraseInitiative(text, context, out championLine)
            || TryPhrasePlay(text, context, out championLine)
            || TryPhraseBattlefieldEntry(text, context, out championLine)
            || TryPhraseChampionAttack(text, context, out championLine)
            || TryPhraseUnitAttack(text, context, out championLine)
            || TryPhraseChampionDamage(text, context, out championLine)
            || TryPhraseUnitDamage(text, context, out championLine)
            || TryPhraseWinCheck(text, context, out championLine)
            || TryPhraseResolve(text, context, out championLine))
        {
            return championLine;
        }

        text = ReplaceIdentifiers(text, context);
        text = RemoveCardCodes(text);

        return text switch
        {
            "Attack Step begins." => "Attack step begins.",
            var value when value.Contains("Enter Overtime", StringComparison.Ordinal) => "Both champions fall. Overtime begins.",
            _ => text,
        };
    }

    private MatchSetup BuildSetup(World world, Tarn.Domain.Match fixture)
    {
        if (fixture.ReplaySetup is not null)
        {
            return BuildStoredSetup(world, fixture);
        }

        var home = world.Players[fixture.HomePlayerId];
        var away = world.Players[fixture.AwayPlayerId];
        var homeDeck = home.ActiveDeck ?? throw new InvalidOperationException($"Player '{home.Id}' has no deck.");
        var awayDeck = away.ActiveDeck ?? throw new InvalidOperationException($"Player '{away.Id}' has no deck.");
        var homeChampion = (ChampionCardDefinition)world.GetLatestDefinition(home.Collection.First(card => card.InstanceId == homeDeck.ChampionInstanceId).CardId);
        var awayChampion = (ChampionCardDefinition)world.GetLatestDefinition(away.Collection.First(card => card.InstanceId == awayDeck.ChampionInstanceId).CardId);
        var homeCards = homeDeck.NonChampionInstanceIds.Select(id => world.GetLatestDefinition(home.Collection.First(card => card.InstanceId == id).CardId)).ToList();
        var awayCards = awayDeck.NonChampionInstanceIds.Select(id => world.GetLatestDefinition(away.Collection.First(card => card.InstanceId == id).CardId)).ToList();
        var seed = ResolveMatchSeed(world, fixture);

        return new MatchSetup
        {
            Seed = seed,
            PlayerOneId = fixture.HomePlayerId,
            PlayerTwoId = fixture.AwayPlayerId,
            PlayerOneDeck = new DeckDefinition(homeChampion, homeCards),
            PlayerTwoDeck = new DeckDefinition(awayChampion, awayCards),
            ShuffleDecks = true,
            Initiative = simulator.BuildInitiativeContext(fixture),
        };
    }

    private static MatchSetup BuildStoredSetup(World world, Tarn.Domain.Match fixture)
    {
        var replaySetup = fixture.ReplaySetup ?? throw new InvalidOperationException($"Fixture '{fixture.Id}' is missing replay setup.");
        return new MatchSetup
        {
            Seed = replaySetup.Seed,
            PlayerOneId = fixture.HomePlayerId,
            PlayerTwoId = fixture.AwayPlayerId,
            PlayerOneDeck = new DeckDefinition(
                (ChampionCardDefinition)world.GetLatestDefinition(replaySetup.HomeDeck.ChampionCardId),
                replaySetup.HomeDeck.NonChampionCardIds.Select(world.GetLatestDefinition).ToList()),
            PlayerTwoDeck = new DeckDefinition(
                (ChampionCardDefinition)world.GetLatestDefinition(replaySetup.AwayDeck.ChampionCardId),
                replaySetup.AwayDeck.NonChampionCardIds.Select(world.GetLatestDefinition).ToList()),
            ShuffleDecks = true,
            Initiative = replaySetup.Initiative,
        };
    }

    private static int ResolveMatchSeed(World world, Tarn.Domain.Match fixture)
    {
        var stepSeed = (fixture.Year * 1000) + fixture.Week;
        if (fixture.Year == world.Season.Year)
        {
            stepSeed = (fixture.Year * 1000) + fixture.Week;
        }

        return stepSeed + fixture.FixturePriority;
    }

    private static string ResolveInitialPlayerId(Tarn.Domain.Match fixture)
    {
        if (fixture.Phase == MatchPhase.Playoffs && fixture.HomeSeed is not null && fixture.AwaySeed is not null)
        {
            return fixture.HomeSeed <= fixture.AwaySeed ? fixture.HomePlayerId : fixture.AwayPlayerId;
        }

        return fixture.FixturePriority % 2 == 1 ? fixture.HomePlayerId : fixture.AwayPlayerId;
    }

    private static RoundSnapshotViewModel CaptureSnapshot(World world, Tarn.Domain.Match fixture, MatchState state, int lastLogIndexExclusive)
    {
        var playerOne = state.PlayerOne;
        var playerTwo = state.PlayerTwo;
        var homePlayer = world.Players[fixture.HomePlayerId];
        var awayPlayer = world.Players[fixture.AwayPlayerId];
        return new RoundSnapshotViewModel(
            Math.Max(0, state.RoundNumber),
            state.RoundNumber <= 0 ? "Setup" : state.RoundNumber.ToString(),
            state.WinnerPlayerId is not null ? "Complete" : state.OvertimePending ? "Overtime Pending" : state.RoundNumber <= 0 ? "Opening" : "In Progress",
            state.InitiativePlayerIndex == 0 ? homePlayer.Name : awayPlayer.Name,
            new ChampionPanelViewModel("Home", homePlayer.Name, playerOne.Champion.Card.Name, playerOne.Champion.Health, playerOne.FatigueCount),
            new ChampionPanelViewModel("Away", awayPlayer.Name, playerTwo.Champion.Card.Name, playerTwo.Champion.Health, playerTwo.FatigueCount),
            BuildBattlefieldLines(playerOne.Battlefield),
            BuildBattlefieldLines(playerTwo.Battlefield),
            FormatCounters(playerOne.CounterZone),
            FormatCounters(playerTwo.CounterZone),
            lastLogIndexExclusive);
    }

    private static IReadOnlyList<string> BuildBattlefieldLines(IReadOnlyList<UnitState> units)
    {
        if (units.Count == 0)
        {
            return ["empty"];
        }

        return units
            .OrderBy(unit => unit.ZoneOrder)
            .Select((unit, index) => FormatUnit(index + 1, unit))
            .ToList();
    }

    private static IReadOnlyList<string> BuildReplayInfoLines(MatchState state)
    {
        return
        [
            $"Seed: {state.Seed}",
            $"Home champion: {state.PlayerOne.Champion.Card.Name}",
            $"Away champion: {state.PlayerTwo.Champion.Card.Name}",
            $"Decks: {state.PlayerOne.Deck.MainDeck.Count} vs {state.PlayerTwo.Deck.MainDeck.Count} cards",
        ];
    }

    private static string FormatUnit(int slot, UnitState unit)
    {
        var tags = new List<string>();
        if (unit.HasDefender)
        {
            tags.Add("[Def]");
        }

        if (unit.HasMagnet)
        {
            tags.Add("[Mag]");
        }

        var tagText = tags.Count == 0 ? string.Empty : " " + string.Join(" ", tags);
        return $"[{slot}] {unit.Card.Name} {unit.Attack}/{unit.Health}{tagText}";
    }

    private static string FormatCounters(IReadOnlyList<CounterState> counters)
    {
        if (counters.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", counters.OrderBy(counter => counter.ZoneOrder).Select(counter => counter.Card.Name));
    }

    private static bool TryPhraseChampionLine(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " Champion: ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var playerLabel = ResolvePlayerLabel(text[..markerIndex], context);
        var champion = RemoveCardCodes(text[(markerIndex + marker.Length)..]);
        var statsIndex = champion.IndexOf(" (", StringComparison.Ordinal);
        if (statsIndex >= 0)
        {
            champion = champion[..statsIndex];
        }

        result = $"{playerLabel} champion: {champion.Trim()}.";
        return true;
    }

    private static bool TryPhraseDeckLine(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " Deck: ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        result = $"{ResolvePlayerLabel(text[..markerIndex], context)} deck ready.";
        return true;
    }

    private static bool TryPhraseRoundStart(string text, out string result)
    {
        if (!text.StartsWith("=== Round ", StringComparison.Ordinal) || !text.EndsWith("===", StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        result = text.Trim('=').Trim() + " begins.";
        return true;
    }

    private static bool TryPhraseInitiative(string text, ReplayPhraseContext context, out string result)
    {
        const string roundMarker = " initiative: ";
        var roundMarkerIndex = text.IndexOf(roundMarker, StringComparison.Ordinal);
        if (roundMarkerIndex >= 0)
        {
            result = $"{ResolvePlayerLabel(text[(roundMarkerIndex + roundMarker.Length)..], context)} has initiative.";
            return true;
        }

        const string passMarker = "Initiative passes to ";
        if (text.StartsWith(passMarker, StringComparison.Ordinal))
        {
            result = $"Initiative passes to {ResolvePlayerLabel(text[passMarker.Length..].TrimEnd('.'), context)}.";
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static bool TryPhrasePlay(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " plays ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var playerLabel = ResolvePlayerLabel(text[..markerIndex], context);
        var cardText = RemoveCardCodes(text[(markerIndex + marker.Length)..]).Trim();
        result = $"{playerLabel} plays {cardText}";
        return true;
    }

    private static bool TryPhraseBattlefieldEntry(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " enters the Battlefield as ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        result = $"{ResolveInstanceLabel(text[..markerIndex], context)} enters as {text[(markerIndex + marker.Length)..]}";
        return true;
    }

    private static bool TryPhraseChampionAttack(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = "'s Champion attacks ";
        const string defenderMarker = "'s Champion for ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || !text.Contains(defenderMarker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        var attacker = ResolvePlayerLabel(text[..markerIndex], context);
        var remaining = text[(markerIndex + marker.Length)..];
        var defenderMarkerIndex = remaining.IndexOf(defenderMarker, StringComparison.Ordinal);
        var defender = ResolvePlayerLabel(remaining[..defenderMarkerIndex], context);
        var damage = remaining[(defenderMarkerIndex + defenderMarker.Length)..];
        result = $"{attacker} attacks {defender} for {damage}";
        return true;
    }

    private static bool TryPhraseUnitAttack(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " attacks ";
        const string defenderMarker = "'s Champion for ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || !text.Contains(defenderMarker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        var attacker = ResolveInstanceLabel(text[..markerIndex], context);
        var remaining = text[(markerIndex + marker.Length)..];
        var defenderMarkerIndex = remaining.IndexOf(defenderMarker, StringComparison.Ordinal);
        if (defenderMarkerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var defender = ResolvePlayerLabel(remaining[..defenderMarkerIndex], context);
        var damage = remaining[(defenderMarkerIndex + defenderMarker.Length)..];
        result = $"{attacker} attacks {defender} for {damage}";
        return true;
    }

    private static bool TryPhraseChampionDamage(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = "'s Champion takes ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        result = $"{ResolvePlayerLabel(text[..markerIndex], context)} takes {text[(markerIndex + marker.Length)..]}";
        return true;
    }

    private static bool TryPhraseUnitDamage(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " takes ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= 0 || !text.Contains(" damage and falls to ", StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        result = $"{ResolveInstanceLabel(text[..markerIndex], context)} takes {text[(markerIndex + marker.Length)..]}";
        return true;
    }

    private static bool TryPhraseWinCheck(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = "Win check: ";
        if (!text.StartsWith(marker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        var entries = text[marker.Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length != 2)
        {
            result = ReplaceIdentifiers(text, context);
            return true;
        }

        var homeHealth = ParseHealth(entries[0]);
        var awayHealth = ParseHealth(entries[1]);
        result = (homeHealth > 0, awayHealth > 0) switch
        {
            (true, true) => "Win check: both champions survive.",
            (false, false) => "Win check: both champions fall.",
            (true, false) => $"Win check: {context.HomeLabel} survives.",
            _ => $"Win check: {context.AwayLabel} survives.",
        };
        return true;
    }

    private static bool TryPhraseResolve(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = "Resolve: ";
        if (!text.StartsWith(marker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        var detail = ReplaceIdentifiers(text[marker.Length..], context);
        detail = RemoveCardCodes(detail);
        var nestedMarkerIndex = detail.LastIndexOf(": ", StringComparison.Ordinal);
        if (nestedMarkerIndex >= 0)
        {
            detail = detail[(nestedMarkerIndex + 2)..];
        }

        detail = detail.Replace("spell ", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        result = $"Resolve: {detail}";
        return true;
    }

    private static string ReplaceIdentifiers(string text, ReplayPhraseContext context)
    {
        var value = ReplaceInstanceIds(text, context);
        value = value
            .Replace(context.HomePlayerId, context.HomeLabel, StringComparison.Ordinal)
            .Replace(context.AwayPlayerId, context.AwayLabel, StringComparison.Ordinal)
            .Replace("P1", context.HomeLabel, StringComparison.Ordinal)
            .Replace("P2", context.AwayLabel, StringComparison.Ordinal);
        return value;
    }

    private static string ReplaceInstanceIds(string text, ReplayPhraseContext context)
        => InstanceIdPattern.Replace(text, match => ResolveInstanceLabel(match.Value, context));

    private static string ResolveInstanceLabel(string instanceId, ReplayPhraseContext context)
    {
        var match = CardIdPattern.Match(instanceId);
        if (!match.Success)
        {
            return instanceId;
        }

        var cardId = match.Value;
        return context.ResolveCardName(cardId) ?? cardId;
    }

    private static string ResolvePlayerLabel(string raw, ReplayPhraseContext context)
    {
        var value = raw.Trim();
        if (string.Equals(value, context.HomePlayerId, StringComparison.Ordinal) || string.Equals(value, "P1", StringComparison.Ordinal))
        {
            return context.HomeLabel;
        }

        if (string.Equals(value, context.AwayPlayerId, StringComparison.Ordinal) || string.Equals(value, "P2", StringComparison.Ordinal))
        {
            return context.AwayLabel;
        }

        return value;
    }

    private static string RemoveCardCodes(string text)
        => Regex.Replace(text, @"\b(?:CH|CT|SP|UN)\d{3}\s+", string.Empty).Trim();

    private static int ParseHealth(string entry)
    {
        var separatorIndex = entry.IndexOf('=');
        return separatorIndex >= 0 && int.TryParse(entry[(separatorIndex + 1)..].TrimEnd('.'), out var health)
            ? health
            : 0;
    }

    private static string? TryResolveCardName(World world, string cardId)
    {
        try
        {
            return world.GetLatestDefinition(cardId).Name;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string? TryResolvePreviewCardName(string cardId) => TryResolveCardName(PreviewWorld.Value, cardId);

    private sealed record ReplayPhraseContext(
        string HomePlayerId,
        string AwayPlayerId,
        string HomeLabel,
        string AwayLabel,
        Func<string, string?> ResolveCardName);
}
