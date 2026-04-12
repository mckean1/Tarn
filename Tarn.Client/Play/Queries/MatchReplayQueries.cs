using Regex = System.Text.RegularExpressions.Regex;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class MatchReplayQueries
{
    private const string LegacyCardIdPatternText = @"(?:CH|CT|SP|UN)\d{3}";
    private const string GeneratedCardIdPatternText = @"[A-Z]{3}\d{3}-(?:CH|CT|SP|UN)\d{2}-[A-Z]";
    private const string CardIdPatternText = $"(?:{GeneratedCardIdPatternText}|{LegacyCardIdPatternText})";

    private static readonly Regex CardIdPattern = new($@"\b{CardIdPatternText}\b");
    private static readonly Regex InstanceIdPattern = new($@"\b[\w]+-{CardIdPatternText}-\d+\b");
    private static readonly Regex CardCodeWithNamePattern = new($@"\b{CardIdPatternText}\s+");
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
            ShapeEventLog(state.ReplayLog, phraseContext).ToList(),
            snapshots);
    }

    public static string PhraseEvent(string raw)
        => PhraseEvent(raw, new ReplayPhraseContext("P1", "P2", "Home", "Away", TryResolvePreviewCardName));

    public static IReadOnlyList<string> ShapeEventLog(IEnumerable<string> rawEvents)
        => ShapeEventLog(rawEvents, new ReplayPhraseContext("P1", "P2", "Home", "Away", TryResolvePreviewCardName)).ToList();

    private static IEnumerable<string> ShapeEventLog(IEnumerable<string> rawEvents, ReplayPhraseContext context)
        => rawEvents.Select(raw => PhraseEvent(raw, context));

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
            || TryPhraseCounterZoneEntry(text, context, out championLine)
            || TryPhraseCounterCommit(text, context, out championLine)
            || TryPhraseChampionAttack(text, context, out championLine)
            || TryPhraseUnitAttack(text, context, out championLine)
            || TryPhraseAssignedToDefender(text, context, out championLine)
            || TryPhraseChampionDamage(text, context, out championLine)
            || TryPhraseUnitDamage(text, context, out championLine)
            || TryPhraseDeathCheck(text, out championLine)
            || TryPhraseLeavesBattlefield(text, context, out championLine)
            || TryPhraseWinCheck(text, context, out championLine)
            || TryPhraseCounterResolution(text, context, out championLine)
            || TryPhrasePreventDamage(text, context, out championLine)
            || TryPhraseCountered(text, context, out championLine)
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
            $"Seed {state.Seed}",
            $"Champions: {state.PlayerOne.Champion.Card.Name} vs {state.PlayerTwo.Champion.Card.Name}",
            $"Decks: {state.PlayerOne.Deck.MainDeck.Count} vs {state.PlayerTwo.Deck.MainDeck.Count}",
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
        return $"[{slot}] {unit.Card.Name,-14} {unit.Attack}/{unit.Health}{tagText}";
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

        result = "Both champions are ready.";
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

        result = string.Empty;
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
        result = EnsureSentence($"{playerLabel} plays {cardText}");
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

        result = EnsureSentence($"{ResolveInstanceLabel(text[..markerIndex], context)} enters as {text[(markerIndex + marker.Length)..]}");
        return true;
    }

    private static bool TryPhraseCounterZoneEntry(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " enters the Counter Zone.";
        if (!text.EndsWith(marker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        result = $"{ResolveInstanceLabel(text[..^marker.Length], context)} is set as a counter.";
        return true;
    }

    private static bool TryPhraseCounterCommit(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " commits from the Counter Zone targeting ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var cardName = ResolveInstanceLabel(text[..markerIndex], context);
        var target = SimplifyEffectDescription(text[(markerIndex + marker.Length)..], context);
        result = string.IsNullOrWhiteSpace(target)
            ? $"{cardName} answers the play."
            : $"{cardName} answers {target}.";
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
        result = EnsureSentence($"{attacker} attacks {defender} for {damage}");
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
        result = EnsureSentence($"{attacker} attacks {defender} for {damage}");
        return true;
    }

    private static bool TryPhraseAssignedToDefender(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " attack damage is assigned to Defender ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var amount = text[..markerIndex].Trim();
        var defender = ResolveInstanceLabel(text[(markerIndex + marker.Length)..].TrimEnd('.'), context);
        result = $"{defender} absorbs {amount} damage.";
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

    private static bool TryPhraseDeathCheck(string text, out string result)
    {
        if (!text.StartsWith("Death check finds ", StringComparison.Ordinal) || !text.EndsWith(" dead.", StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        result = string.Empty;
        return true;
    }

    private static bool TryPhraseLeavesBattlefield(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " leaves the Battlefield and moves to Discard.";
        if (!text.EndsWith(marker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        result = $"{ResolveInstanceLabel(text[..^marker.Length], context)} is destroyed.";
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

        result = EnsureSentence(SimplifyEffectDescription(text[marker.Length..], context));
        return true;
    }

    private static bool TryPhraseCounterResolution(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " counters ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var counter = ResolveCardLabel(text[..markerIndex].Trim(), context);
        var target = SimplifyEffectDescription(text[(markerIndex + marker.Length)..], context);
        result = string.IsNullOrWhiteSpace(target)
            ? $"{counter} counters the play."
            : $"{counter} counters {target}.";
        return true;
    }

    private static bool TryPhrasePreventDamage(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " prevents damage from ";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            result = string.Empty;
            return false;
        }

        var source = ResolveCardLabel(text[..markerIndex].Trim(), context);
        var remainder = text[(markerIndex + marker.Length)..].TrimEnd('.');
        var attacker = ResolveInstanceLabel(remainder.Replace(" this attack", string.Empty, StringComparison.Ordinal), context);
        result = $"{source} blocks {attacker} this attack.";
        return true;
    }

    private static bool TryPhraseCountered(string text, ReplayPhraseContext context, out string result)
    {
        const string marker = " is countered.";
        if (!text.EndsWith(marker, StringComparison.Ordinal))
        {
            result = string.Empty;
            return false;
        }

        var detail = SimplifyEffectDescription(text[..^marker.Length], context);
        result = string.IsNullOrWhiteSpace(detail)
            ? "The play is countered."
            : $"{detail} is countered.";
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
        return ResolveCardLabel(cardId, context);
    }

    private static string ResolveCardLabel(string cardId, ReplayPhraseContext context)
        => context.ResolveCardName(cardId) ?? cardId;

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
        => CardCodeWithNamePattern.Replace(text, string.Empty).Trim();

    private static string SimplifyEffectDescription(string text, ReplayPhraseContext context)
    {
        var value = ReplaceIdentifiers(text.TrimEnd('.'), context);
        value = RemoveCardCodes(value);

        var nestedMarkerIndex = value.LastIndexOf(": ", StringComparison.Ordinal);
        if (nestedMarkerIndex >= 0)
        {
            value = value[(nestedMarkerIndex + 2)..];
        }

        return value
            .Replace("spell ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" counter effect", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string EnsureSentence(string text)
    {
        var value = text.Trim();
        return value.EndsWith(".", StringComparison.Ordinal) ? value : value + ".";
    }

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
