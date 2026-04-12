namespace Tarn.Domain;

public enum CardRarity
{
    Common,
    Rare,
    Epic,
    Legendary,
}

public enum MatchPhase
{
    RegularSeason,
    Playoffs,
}

public enum MatchRoundType
{
    Quarterfinal,
    Semifinal,
    Final,
}

public enum ListingSource
{
    PlayerAuction,
    CollectorSingle,
}

public enum ListingStatus
{
    Active,
    Sold,
    Expired,
    Cancelled,
}

public enum LegendaryState
{
    Unissued,
    RevealedForCollector,
    HiddenCollectorHeld,
    Owned,
}

public enum CollectorPhase
{
    Launch,
    Warm,
    Normal,
    Offseason,
}

public enum PatchClassification
{
    Healthy,
    Watchlist,
    Buff,
    HardBuff,
    Nerf,
    HardNerf,
}

public enum PatchOpType
{
    AttackDelta,
    HealthDelta,
    SpeedDelta,
    PowerDelta,
    AddKeyword,
    RemoveKeyword,
    EffectValueDelta,
    AddDrawback,
    RemoveDrawback,
    AddOncePerRoundLimiter,
}

public sealed record WeekRange(int Start, int End)
{
    public bool Includes(int week) => week >= Start && week <= End;
}

public sealed record PhaseCollectorConfig(
    int Singles,
    int Packs,
    IReadOnlyList<double> SetBias,
    IReadOnlyDictionary<CardType, double> FamilyMix);

public sealed record PackPriceConfig(
    int NewestLaunch,
    int NewestNormal,
    int OlderStandard);

public sealed record SetCompositionConfig(
    int TotalCards,
    int Champions,
    int Units,
    int Spells,
    int Counters,
    IReadOnlyDictionary<CardRarity, int> RarityCounts,
    IReadOnlyDictionary<CardRarity, int> ChampionRarityCounts,
    int LegendaryChampions);

public sealed record TarnEconomyConfig(
    int StartingCash,
    decimal MarketFeeRate,
    IReadOnlyDictionary<CardRarity, int> CollectorBaseSellPrice,
    IReadOnlyDictionary<CardType, decimal> CollectorTypeMultipliers,
    IReadOnlyDictionary<CardRarity, decimal> CollectorBuybackRates,
    PackPriceConfig PackPrices,
    IReadOnlyDictionary<LeagueTier, IReadOnlyList<(int start, int end, int amount)>> Payouts);

public sealed record TarnSeasonConfig(
    int RegularSeasonWeeks,
    int PlayoffWeeks,
    int SeasonCloseWeek,
    int RewardWeek,
    int PatchWeek,
    int GrantWeek,
    int TotalWeeks,
    int StandardRotationDepth,
    int DeckSize,
    int ChampionCount,
    int NonChampionCount,
    int MaxCopiesPerCard,
    int MaxDeckPower);

public sealed record TarnLeagueConfig(
    IReadOnlyList<LeagueTier> LeagueOrder,
    int PlayersPerLeague,
    int DivisionsPerLeague,
    int PlayersPerDivision);

public sealed record PatchThresholdConfig(
    decimal AppearanceBuff,
    decimal AppearanceNerf,
    decimal WinDeltaBuff,
    decimal WinDeltaNerf,
    decimal PlayoffBuff,
    decimal PlayoffNerf,
    decimal MarketBuff,
    decimal MarketNerf,
    decimal RoleBuff,
    decimal RoleNerf);

public sealed record TarnConfig(
    TarnLeagueConfig Leagues,
    TarnSeasonConfig Season,
    SetCompositionConfig Sets,
    TarnEconomyConfig Economy,
    IReadOnlyDictionary<CollectorPhase, PhaseCollectorConfig> CollectorPhases,
    PatchThresholdConfig PatchThresholds)
{
    public static TarnConfig Default { get; } = new(
        Leagues: new TarnLeagueConfig(
            LeagueOrder: [LeagueTier.Bronze, LeagueTier.Silver, LeagueTier.Gold, LeagueTier.World],
            PlayersPerLeague: 20,
            DivisionsPerLeague: 4,
            PlayersPerDivision: 5),
        Season: new TarnSeasonConfig(
            RegularSeasonWeeks: 27,
            PlayoffWeeks: 3,
            SeasonCloseWeek: 31,
            RewardWeek: 32,
            PatchWeek: 33,
            GrantWeek: 34,
            TotalWeeks: 52,
            StandardRotationDepth: 3,
            DeckSize: 31,
            ChampionCount: 1,
            NonChampionCount: 30,
            MaxCopiesPerCard: 3,
            MaxDeckPower: 100),
        Sets: new SetCompositionConfig(
            TotalCards: 100,
            Champions: 20,
            Units: 50,
            Spells: 20,
            Counters: 10,
            RarityCounts: new Dictionary<CardRarity, int>
            {
                [CardRarity.Common] = 52,
                [CardRarity.Rare] = 25,
                [CardRarity.Epic] = 13,
                [CardRarity.Legendary] = 10,
            },
            ChampionRarityCounts: new Dictionary<CardRarity, int>
            {
                [CardRarity.Common] = 9,
                [CardRarity.Rare] = 5,
                [CardRarity.Epic] = 3,
                [CardRarity.Legendary] = 3,
            },
            LegendaryChampions: 3),
        Economy: new TarnEconomyConfig(
            StartingCash: 1500,
            MarketFeeRate: 0.05m,
            CollectorBaseSellPrice: new Dictionary<CardRarity, int>
            {
                [CardRarity.Common] = 40,
                [CardRarity.Rare] = 100,
                [CardRarity.Epic] = 220,
                [CardRarity.Legendary] = 500,
            },
            CollectorTypeMultipliers: new Dictionary<CardType, decimal>
            {
                [CardType.Unit] = 1.0m,
                [CardType.Spell] = 1.0m,
                [CardType.Counter] = 1.1m,
                [CardType.Champion] = 1.4m,
            },
            CollectorBuybackRates: new Dictionary<CardRarity, decimal>
            {
                [CardRarity.Common] = 0.55m,
                [CardRarity.Rare] = 0.50m,
                [CardRarity.Epic] = 0.45m,
                [CardRarity.Legendary] = 0.40m,
            },
            PackPrices: new PackPriceConfig(900, 850, 800),
            Payouts: new Dictionary<LeagueTier, IReadOnlyList<(int start, int end, int amount)>>
            {
                [LeagueTier.Bronze] = [(1, 1, 2200), (2, 2, 1800), (3, 4, 1500), (5, 8, 1200), (9, 12, 1000), (13, 16, 800), (17, 20, 600)],
                [LeagueTier.Silver] = [(1, 1, 3000), (2, 2, 2500), (3, 4, 2100), (5, 8, 1800), (9, 12, 1500), (13, 16, 1200), (17, 20, 900)],
                [LeagueTier.Gold] = [(1, 1, 4100), (2, 2, 3400), (3, 4, 2900), (5, 8, 2500), (9, 12, 2200), (13, 16, 1800), (17, 20, 1300)],
                [LeagueTier.World] = [(1, 1, 5500), (2, 2, 4600), (3, 4, 3900), (5, 8, 3400), (9, 12, 3000), (13, 16, 2500), (17, 20, 1800)],
            }),
        CollectorPhases: new Dictionary<CollectorPhase, PhaseCollectorConfig>
        {
            [CollectorPhase.Launch] = new(48, 16, [0.70, 0.20, 0.10], BuildFamilyMix(0.25, 0.45, 0.20, 0.10)),
            [CollectorPhase.Warm] = new(40, 12, [0.50, 0.30, 0.20], BuildFamilyMix(0.20, 0.50, 0.20, 0.10)),
            [CollectorPhase.Normal] = new(36, 8, [0.34, 0.33, 0.33], BuildFamilyMix(0.20, 0.50, 0.20, 0.10)),
            [CollectorPhase.Offseason] = new(40, 10, [0.34, 0.33, 0.33], BuildFamilyMix(0.20, 0.50, 0.20, 0.10)),
        },
        PatchThresholds: new PatchThresholdConfig(
            AppearanceBuff: 0.08m,
            AppearanceNerf: 0.45m,
            WinDeltaBuff: -0.08m,
            WinDeltaNerf: 0.08m,
            PlayoffBuff: 0.05m,
            PlayoffNerf: 0.30m,
            MarketBuff: 0.10m,
            MarketNerf: 0.55m,
            RoleBuff: 0.10m,
            RoleNerf: 0.50m));

    private static IReadOnlyDictionary<CardType, double> BuildFamilyMix(double champion, double unit, double spell, double counter) =>
        new Dictionary<CardType, double>
        {
            [CardType.Champion] = champion,
            [CardType.Unit] = unit,
            [CardType.Spell] = spell,
            [CardType.Counter] = counter,
        };
}
