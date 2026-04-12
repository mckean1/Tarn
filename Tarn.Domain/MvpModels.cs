using System.Text.Json.Serialization;

namespace Tarn.Domain;

public sealed record BaseTemplate(
    string Key,
    CardType Family,
    int Attack,
    int Health,
    int Speed,
    int PowerBudget,
    int ComplexityBudget,
    IReadOnlyList<string> Keywords);

public sealed record ArchetypeTemplate(
    string Key,
    string Theme,
    CardType Family,
    int AttackDelta,
    int HealthDelta,
    int SpeedDelta,
    int EffectValue,
    IReadOnlyList<string> PreferredKeywords);

public sealed record ModifierPass(
    string Key,
    int AttackDelta,
    int HealthDelta,
    int SpeedDelta,
    int PowerDelta,
    int ComplexityDelta,
    IReadOnlyList<string> AddedKeywords,
    IReadOnlyList<string> RemovedKeywords);

public sealed record CardGenerationRecipe(
    string SetId,
    string CardId,
    string Name,
    CardType Family,
    CardRarity Rarity,
    BaseTemplate BaseTemplate,
    ArchetypeTemplate ArchetypeTemplate,
    IReadOnlyList<ModifierPass> Modifiers,
    string RoleKey,
    int Sequence);

public sealed record ValidationResult(bool IsValid, string? Error = null);

public sealed record CardVersion(
    string CardId,
    int Version,
    CardDefinition Definition,
    int Attack,
    int Health,
    int Speed,
    int Power,
    IReadOnlyList<string> Keywords,
    string RulesText);

public sealed class CardSet
{
    public required string Id { get; init; }
    public required int Sequence { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> Keywords { get; init; }
    public required IReadOnlyList<string> CardIds { get; init; }
    public required HashSet<string> UnissuedLegendaryIds { get; init; }
    public required HashSet<string> HiddenCollectorLegendaryIds { get; init; }
}

public sealed class OwnedCard
{
    public required string InstanceId { get; init; }
    public required string CardId { get; init; }
    public required int Version { get; set; }
    public bool IsListed { get; set; }
    public bool PendingSettlement { get; set; }
}

public sealed class SubmittedDeck
{
    public required string PlayerId { get; init; }
    public required string ChampionInstanceId { get; init; }
    public required IReadOnlyList<string> NonChampionInstanceIds { get; init; }
    public required int SubmittedWeek { get; init; }
    public string? Label { get; init; }
}

public sealed class Player
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required LeagueTier League { get; set; }
    public required string DivisionId { get; set; }
    public int Cash { get; set; }
    public List<OwnedCard> Collection { get; init; } = [];
    public SubmittedDeck? ActiveDeck { get; set; }
    public List<SubmittedDeck> SavedDecks { get; init; } = [];
    public bool IsHuman { get; set; }
}

public sealed class Division
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required LeagueTier League { get; init; }
    public List<string> PlayerIds { get; init; } = [];
}

public sealed class Match
{
    public required string Id { get; init; }
    public required int Year { get; init; }
    public required int Week { get; init; }
    public required LeagueTier League { get; init; }
    public required string DivisionId { get; init; }
    public required string HomePlayerId { get; init; }
    public required string AwayPlayerId { get; init; }
    public required int FixturePriority { get; init; }
    public required MatchPhase Phase { get; init; }
    public MatchRoundType? PlayoffRound { get; init; }
    public int? HomeSeed { get; init; }
    public int? AwaySeed { get; init; }
    public MatchResult? Result { get; set; }
    public HistoricalMatchSetup? ReplaySetup { get; set; }
}

public sealed class HistoricalMatchSetup
{
    public required int Seed { get; init; }
    public required HistoricalDeckSetup HomeDeck { get; init; }
    public required HistoricalDeckSetup AwayDeck { get; init; }
    public required InitiativeContext Initiative { get; init; }
}

public sealed class HistoricalDeckSetup
{
    public required string ChampionCardId { get; init; }
    public required List<string> NonChampionCardIds { get; init; }
}

public sealed class Season
{
    public required int Year { get; init; }
    public required int CurrentWeek { get; set; }
    public required bool StatsLocked { get; set; }
    public List<Match> Schedule { get; init; } = [];
    public Dictionary<string, StandingsEntry> Standings { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<LeagueTier, List<string>> FinalPlacements { get; init; } = [];
    public Dictionary<string, CardUsageStats> CardStats { get; init; } = new(StringComparer.Ordinal);
}

public sealed class CompletedSeasonStats
{
    public int SeasonYear { get; init; }
    public bool IsFrozen { get; init; }
    public Dictionary<string, CardUsageStats> CardStats { get; init; } = new(StringComparer.Ordinal);
}

public sealed class CollectorSingleOffer
{
    public required string ListingId { get; init; }
    public required string CardId { get; init; }
    public required int Version { get; init; }
    public required int Price { get; init; }
    public required bool IsLegendaryReveal { get; init; }
}

public sealed class PackProduct
{
    public required string ProductId { get; init; }
    public required string SetId { get; init; }
    public required int Price { get; init; }
}

public sealed class CollectorInventory
{
    public List<CollectorSingleOffer> Singles { get; init; } = [];
    public List<PackProduct> Packs { get; init; } = [];
    public Dictionary<string, LegendaryState> LegendaryStates { get; init; } = new(StringComparer.Ordinal);
    public int RefreshedWeek { get; set; }
}

public sealed class Bid
{
    public required string PlayerId { get; init; }
    public required int Amount { get; init; }
}

public sealed class MarketListing
{
    public required string Id { get; init; }
    public required ListingSource Source { get; init; }
    public required string CardId { get; init; }
    public required int Version { get; init; }
    public string? CardInstanceId { get; init; }
    public string? SellerPlayerId { get; init; }
    public int? BuyNowPrice { get; init; }
    public int? CollectorPrice { get; init; }
    public required int MinimumBid { get; init; }
    public required int CreatedWeek { get; init; }
    public required int ExpiresWeek { get; init; }
    public ListingStatus Status { get; set; }
    public List<Bid> Bids { get; init; } = [];
}

public sealed record CardPatchOp(PatchOpType Type, int? NumericDelta = null, string? Keyword = null, string? Description = null);

public sealed record PatchResult(
    string CardId,
    int PreviousVersion,
    int NewVersion,
    PatchClassification Classification,
    IReadOnlyList<CardPatchOp> Operations);

public sealed class World
{
    public required TarnConfig Config { get; init; }
    public required Season Season { get; set; }
    public CompletedSeasonStats? LastCompletedSeasonStats { get; set; }
    public Dictionary<LeagueTier, LeagueState> Leagues { get; init; } = [];
    public Dictionary<string, Player> Players { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, Division> Divisions { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, CardSet> CardSets { get; init; } = new(StringComparer.Ordinal);
    public List<string> StandardSetIds { get; init; } = [];
    public Dictionary<string, List<CardVersion>> CardVersions { get; init; } = new(StringComparer.Ordinal);
    public List<MarketListing> MarketListings { get; init; } = [];
    public CollectorInventory CollectorInventory { get; set; } = new();
    public List<PatchResult> PatchHistory { get; init; } = [];
    public int NextCardInstanceNumber { get; set; } = 1;
    public int NewestSetReleaseYear { get; set; }
    public int NewestSetReleaseWeek { get; set; }
}

public sealed class LeagueState
{
    public required LeagueTier Tier { get; init; }
    public List<string> DivisionIds { get; init; } = [];
}

public sealed class CardUsageStats
{
    public string CardId { get; init; } = string.Empty;
    public int DeckAppearances { get; set; }
    public int MatchWins { get; set; }
    public int MatchLosses { get; set; }
    public int PlayoffDeckAppearances { get; set; }
    public int MarketDemand { get; set; }
    public int RoleDemand { get; set; }

    [JsonIgnore]
    public int TotalMatches => MatchWins + MatchLosses;
}

public sealed record InitiativeContext(
    string HomePlayerId,
    string AwayPlayerId,
    int FixturePriority,
    bool IsPlayoff,
    int? HomeSeed,
    int? AwaySeed);
