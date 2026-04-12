namespace Tarn.Domain;

public static class CollectorService
{
    public static void Refresh(World world, int seed)
    {
        var rng = new SeededRng(seed + (world.Season.Year * 1000) + world.Season.CurrentWeek);
        var phase = GetPhase(world);
        var phaseConfig = world.Config.CollectorPhases[phase];
        var justHidden = new HashSet<string>(StringComparer.Ordinal);

        foreach (var single in world.CollectorInventory.Singles.Where(single => single.IsLegendaryReveal))
        {
            world.CollectorInventory.LegendaryStates[single.CardId] = LegendaryState.HiddenCollectorHeld;
            var setId = world.GetLatestDefinition(single.CardId).SetId;
            world.CardSets[setId].UnissuedLegendaryIds.Remove(single.CardId);
            world.CardSets[setId].HiddenCollectorLegendaryIds.Add(single.CardId);
            justHidden.Add(single.CardId);
        }

        world.CollectorInventory.Singles.Clear();
        world.CollectorInventory.Packs.Clear();

        for (var index = 0; index < phaseConfig.Singles; index++)
        {
            var family = PickFamily(phaseConfig.FamilyMix, rng);
            var setId = PickSet(world, phaseConfig.SetBias, rng);
            var cardId = PickCollectorSingleCard(world, setId, family, rng, justHidden, out var isLegendaryReveal);
            if (cardId is null)
            {
                continue;
            }

            if (isLegendaryReveal)
            {
                world.CollectorInventory.LegendaryStates[cardId] = LegendaryState.RevealedForCollector;
                world.CardSets[setId].UnissuedLegendaryIds.Remove(cardId);
                world.CardSets[setId].HiddenCollectorLegendaryIds.Remove(cardId);
            }

            world.CollectorInventory.Singles.Add(new CollectorSingleOffer
            {
                ListingId = $"COL-S-{world.Season.Year:000}-{world.Season.CurrentWeek:00}-{index + 1:000}",
                CardId = cardId,
                Version = world.GetLatestVersion(cardId).Version,
                Price = GetCollectorSellPrice(world, cardId),
                IsLegendaryReveal = isLegendaryReveal,
            });
        }

        for (var index = 0; index < phaseConfig.Packs; index++)
        {
            var setId = PickSet(world, phaseConfig.SetBias, rng);
            world.CollectorInventory.Packs.Add(new PackProduct
            {
                ProductId = $"COL-P-{world.Season.Year:000}-{world.Season.CurrentWeek:00}-{index + 1:000}",
                SetId = setId,
                Price = GetPackPrice(world, setId),
            });
        }

        world.CollectorInventory.RefreshedWeek = world.Season.CurrentWeek;
    }

    public static CollectorPhase GetPhase(World world)
    {
        var weeksSinceRelease = GetWeeksSinceNewestSetRelease(world);
        if (weeksSinceRelease is >= 0 and <= 3)
        {
            return CollectorPhase.Launch;
        }

        if (weeksSinceRelease is >= 4 and <= 7)
        {
            return CollectorPhase.Warm;
        }

        if (world.Season.CurrentWeek >= 35)
        {
            return CollectorPhase.Offseason;
        }

        return CollectorPhase.Normal;
    }

    public static int GetCollectorSellPrice(World world, string cardId)
    {
        var definition = world.GetLatestDefinition(cardId);
        var basePrice = world.Config.Economy.CollectorBaseSellPrice[definition.Rarity];
        var typeMultiplier = world.Config.Economy.CollectorTypeMultipliers[definition.Type];
        var metaModifier = Math.Clamp(0.95m + (GetMarketPressure(world, cardId) * 0.02m), 0.85m, 1.25m);
        var newestSetId = world.StandardSetIds.Last();
        decimal releaseModifier = 1.00m;
        if (string.Equals(definition.SetId, newestSetId, StringComparison.Ordinal))
        {
            releaseModifier = GetPhase(world) switch
            {
                CollectorPhase.Launch => 1.10m,
                CollectorPhase.Warm => 1.05m,
                _ => 1.00m,
            };
        }

        return (int)Math.Round(basePrice * typeMultiplier * metaModifier * releaseModifier, MidpointRounding.AwayFromZero);
    }

    public static int GetCollectorBuybackPrice(World world, string cardId)
    {
        var definition = world.GetLatestDefinition(cardId);
        var sellPrice = GetCollectorSellPrice(world, cardId);
        return (int)Math.Round(sellPrice * world.Config.Economy.CollectorBuybackRates[definition.Rarity], MidpointRounding.AwayFromZero);
    }

    public static bool BuySingle(World world, string playerId, string listingId)
    {
        var player = world.Players[playerId];
        var listing = world.CollectorInventory.Singles.FirstOrDefault(single => string.Equals(single.ListingId, listingId, StringComparison.Ordinal));
        if (listing is null || player.Cash < listing.Price)
        {
            return false;
        }

        player.Cash -= listing.Price;
        WorldFactory.GrantCard(world, player, listing.CardId);
        world.CollectorInventory.Singles.Remove(listing);
        return true;
    }

    public static bool SellToCollector(World world, string playerId, string cardInstanceId)
    {
        var player = world.Players[playerId];
        var owned = player.Collection.FirstOrDefault(card => string.Equals(card.InstanceId, cardInstanceId, StringComparison.Ordinal));
        if (owned is null || owned.IsListed || owned.PendingSettlement)
        {
            return false;
        }

        if (!CanRemoveCardAndKeepDeck(world, player, owned.InstanceId))
        {
            return false;
        }

        player.Collection.Remove(owned);
        player.Cash += GetCollectorBuybackPrice(world, owned.CardId);
        return true;
    }

    public static IReadOnlyList<OwnedCard> BuyPack(World world, string playerId, string productId, int seed)
    {
        var player = world.Players[playerId];
        var product = world.CollectorInventory.Packs.FirstOrDefault(pack => string.Equals(pack.ProductId, productId, StringComparison.Ordinal));
        if (product is null || player.Cash < product.Price)
        {
            return [];
        }

        player.Cash -= product.Price;
        world.CollectorInventory.Packs.Remove(product);
        return OpenPack(world, player, product.SetId, seed);
    }

    public static IReadOnlyList<OwnedCard> OpenPack(World world, Player player, string setId, int seed)
    {
        var rng = new SeededRng(seed);
        var set = world.CardSets[setId];
        var pool = set.CardIds.Select(world.GetLatestDefinition).ToList();
        var awarded = new List<OwnedCard>(10);

        void AddRandom(CardRarity rarity, int count, bool allowLegendaryUpgrade = false)
        {
            for (var index = 0; index < count; index++)
            {
                CardDefinition? card = null;
                if (allowLegendaryUpgrade && rng.NextInt(10) == 0)
                {
                    var legendaryId = PickUnissuedLegendary(world, setId, rng);
                    if (legendaryId is not null)
                    {
                        card = world.GetLatestDefinition(legendaryId);
                        world.CollectorInventory.LegendaryStates[legendaryId] = LegendaryState.Owned;
                        set.UnissuedLegendaryIds.Remove(legendaryId);
                    }
                }

                card ??= pool
                    .Where(definition => definition.Rarity == rarity)
                    .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                    .ElementAt(rng.NextInt(pool.Count(definition => definition.Rarity == rarity)));
                awarded.Add(WorldFactory.GrantCard(world, player, card.Id));
            }
        }

        AddRandom(CardRarity.Common, 5);
        AddRandom(CardRarity.Rare, 3);
        AddRandom(CardRarity.Epic, 2, allowLegendaryUpgrade: true);

        return awarded;
    }

    public static int GetPackPrice(World world, string setId)
    {
        var newest = world.StandardSetIds.Last();
        if (!string.Equals(setId, newest, StringComparison.Ordinal))
        {
            return world.Config.Economy.PackPrices.OlderStandard;
        }

        return GetPhase(world) == CollectorPhase.Launch
            ? world.Config.Economy.PackPrices.NewestLaunch
            : world.Config.Economy.PackPrices.NewestNormal;
    }

    public static int GetWeeksSinceNewestSetRelease(World world)
    {
        var yearDelta = world.Season.Year - world.NewestSetReleaseYear;
        return (yearDelta * world.Config.Season.TotalWeeks) + (world.Season.CurrentWeek - world.NewestSetReleaseWeek);
    }

    private static decimal GetMarketPressure(World world, string cardId)
    {
        return world.Season.CardStats.TryGetValue(cardId, out var stats)
            ? Math.Min(15, stats.MarketDemand) / 15m
            : 0m;
    }

    private static CardType PickFamily(IReadOnlyDictionary<CardType, double> familyMix, SeededRng rng)
    {
        var roll = rng.NextInt(1000) / 1000d;
        var cumulative = 0d;
        foreach (var pair in familyMix)
        {
            cumulative += pair.Value;
            if (roll <= cumulative)
            {
                return pair.Key;
            }
        }

        return CardType.Unit;
    }

    private static string PickSet(World world, IReadOnlyList<double> bias, SeededRng rng)
    {
        var standard = world.StandardSetIds.ToList();
        var ordered = standard.OrderByDescending(setId => world.CardSets[setId].Sequence).ToList();
        var roll = rng.NextInt(1000) / 1000d;
        var cumulative = 0d;
        for (var index = 0; index < ordered.Count && index < bias.Count; index++)
        {
            cumulative += bias[index];
            if (roll <= cumulative)
            {
                return ordered[index];
            }
        }

        return ordered.Last();
    }

    private static string? PickCollectorSingleCard(World world, string setId, CardType family, SeededRng rng, ISet<string> justHidden, out bool isLegendaryReveal)
    {
        var set = world.CardSets[setId];
        var hiddenLegendaryIds = set.HiddenCollectorLegendaryIds
            .Where(cardId => !justHidden.Contains(cardId))
            .OrderBy(cardId => cardId, StringComparer.Ordinal)
            .ToList();
        if (hiddenLegendaryIds.Count > 0 && family == CardType.Champion)
        {
            isLegendaryReveal = true;
            return hiddenLegendaryIds[rng.NextInt(hiddenLegendaryIds.Count)];
        }

        var pool = set.CardIds
            .Select(world.GetLatestDefinition)
            .Where(card => card.Type == family)
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .ToList();

        var legendaryPool = pool.Where(card => card.Rarity == CardRarity.Legendary && set.UnissuedLegendaryIds.Contains(card.Id)).ToList();
        if (legendaryPool.Count > 0 && rng.NextInt(10) == 0)
        {
            isLegendaryReveal = true;
            return legendaryPool[rng.NextInt(legendaryPool.Count)].Id;
        }

        var nonLegendaryPool = pool.Where(card => card.Rarity != CardRarity.Legendary).ToList();
        isLegendaryReveal = false;
        return nonLegendaryPool.Count == 0 ? null : nonLegendaryPool[rng.NextInt(nonLegendaryPool.Count)].Id;
    }

    private static string? PickUnissuedLegendary(World world, string setId, SeededRng rng)
    {
        var pool = world.CardSets[setId].UnissuedLegendaryIds.OrderBy(cardId => cardId, StringComparer.Ordinal).ToList();
        return pool.Count == 0 ? null : pool[rng.NextInt(pool.Count)];
    }

    private static bool CanRemoveCardAndKeepDeck(World world, Player player, string instanceId)
    {
        var owned = player.Collection.First(card => string.Equals(card.InstanceId, instanceId, StringComparison.Ordinal));
        owned.IsListed = true;
        try
        {
            return DeckBuilder.TryBuildBestDeck(world, player, out _);
        }
        finally
        {
            owned.IsListed = false;
        }
    }
}

public static class MarketService
{
    public static MarketListing? CreateAuctionListing(World world, string playerId, string cardInstanceId, int minimumBid)
    {
        var player = world.Players[playerId];
        var owned = player.Collection.FirstOrDefault(card => string.Equals(card.InstanceId, cardInstanceId, StringComparison.Ordinal));
        if (owned is null || owned.IsListed || owned.PendingSettlement)
        {
            return null;
        }

        if (!CanList(world, player, owned))
        {
            return null;
        }

        owned.IsListed = true;
        var listing = new MarketListing
        {
            Id = $"AUC-{world.Season.Year:000}-{world.Season.CurrentWeek:00}-{world.MarketListings.Count + 1:000}",
            Source = ListingSource.PlayerAuction,
            CardId = owned.CardId,
            Version = owned.Version,
            CardInstanceId = owned.InstanceId,
            SellerPlayerId = playerId,
            MinimumBid = minimumBid,
            CreatedWeek = world.Season.CurrentWeek,
            ExpiresWeek = world.Season.CurrentWeek == world.Config.Season.TotalWeeks ? 1 : world.Season.CurrentWeek + 1,
            Status = ListingStatus.Active,
        };
        world.MarketListings.Add(listing);
        return listing;
    }

    public static bool PlaceBid(World world, string playerId, string listingId, int amount)
    {
        var listing = world.MarketListings.FirstOrDefault(item => string.Equals(item.Id, listingId, StringComparison.Ordinal) && item.Status == ListingStatus.Active);
        if (listing is null || string.Equals(listing.SellerPlayerId, playerId, StringComparison.Ordinal))
        {
            return false;
        }

        var player = world.Players[playerId];
        var highest = GetNextBidAmount(listing);
        if (amount < highest || GetAvailableCashForBids(world, playerId, listing.Id) < amount)
        {
            return false;
        }

        listing.Bids.RemoveAll(bid => string.Equals(bid.PlayerId, playerId, StringComparison.Ordinal));
        listing.Bids.Add(new Bid { PlayerId = playerId, Amount = amount });
        if (world.Season.CardStats.TryGetValue(listing.CardId, out var stats))
        {
            stats.MarketDemand++;
        }

        return true;
    }

    public static void SettleWeek(World world)
    {
        foreach (var listing in world.MarketListings.Where(listing => listing.Status == ListingStatus.Active && listing.ExpiresWeek <= world.Season.CurrentWeek).ToList())
        {
            var seller = world.Players[listing.SellerPlayerId!];
            var owned = seller.Collection.First(card => string.Equals(card.InstanceId, listing.CardInstanceId, StringComparison.Ordinal));
            var winner = listing.Bids
                .OrderByDescending(bid => bid.Amount)
                .ThenBy(bid => bid.PlayerId, StringComparer.Ordinal)
                .FirstOrDefault(bid => world.Players[bid.PlayerId].Cash >= bid.Amount);

            if (winner is null)
            {
                owned.IsListed = false;
                listing.Status = ListingStatus.Expired;
                continue;
            }

            var buyer = world.Players[winner.PlayerId];
            if (buyer.Cash < winner.Amount)
            {
                owned.IsListed = false;
                listing.Status = ListingStatus.Expired;
                continue;
            }

            buyer.Cash -= winner.Amount;
            seller.Cash += winner.Amount - (int)Math.Round(winner.Amount * world.Config.Economy.MarketFeeRate, MidpointRounding.AwayFromZero);
            seller.Collection.Remove(owned);
            owned.IsListed = false;
            owned.PendingSettlement = false;
            buyer.Collection.Add(owned);
            listing.Status = ListingStatus.Sold;
        }
    }

    public static int GetNextBidAmount(MarketListing listing)
    {
        return listing.Bids.Count == 0 ? listing.MinimumBid : listing.Bids.Max(bid => bid.Amount) + 1;
    }

    public static int GetAvailableCashForBids(World world, string playerId, string? excludingListingId = null)
    {
        var player = world.Players[playerId];
        return player.Cash - GetCommittedCash(world, playerId, excludingListingId);
    }

    private static int GetCommittedCash(World world, string playerId, string? excludingListingId)
    {
        return world.MarketListings
            .Where(listing => listing.Status == ListingStatus.Active)
            .Where(listing => !string.Equals(listing.Id, excludingListingId, StringComparison.Ordinal))
            .Select(listing => listing.Bids
                .Where(bid => string.Equals(bid.PlayerId, playerId, StringComparison.Ordinal))
                .OrderByDescending(bid => bid.Amount)
                .FirstOrDefault())
            .Where(bid => bid is not null)
            .Sum(bid => bid!.Amount);
    }

    private static bool CanList(World world, Player player, OwnedCard card)
    {
        card.IsListed = true;
        try
        {
            return DeckBuilder.TryBuildBestDeck(world, player, out _);
        }
        finally
        {
            card.IsListed = false;
        }
    }
}
