namespace Tarn.Domain;

public static class WorldCardExtensions
{
    public static CardVersion GetLatestVersion(this World world, string cardId) =>
        world.CardVersions.TryGetValue(cardId, out var versions)
            ? versions.OrderByDescending(version => version.Version).First()
            : throw new KeyNotFoundException($"Unknown card id '{cardId}'.");

    public static CardDefinition GetLatestDefinition(this World world, string cardId) => world.GetLatestVersion(cardId).Definition;
}

public sealed class CardGenerator
{
    private static readonly string[] EvergreenKeywords = ["Defender", "Magnet", "Swift", "Ward", "Fury"];
    private static readonly string[] Themes = ["Ash", "Iron", "Bloom", "Storm", "Echo", "Grave", "Radiant", "Null"];

    private readonly TarnConfig config;

    public CardGenerator(TarnConfig? config = null)
    {
        this.config = config ?? TarnConfig.Default;
    }

    public CardSet GenerateSet(int setSequence)
    {
        var setId = $"SET{setSequence:000}";
        var recipes = BuildRecipes(setId, setSequence);
        var versions = recipes
            .Select(recipe => Resolve(recipe))
            .ToList();

        return new CardSet
        {
            Id = setId,
            Sequence = setSequence,
            Name = $"Tarn Set {setSequence}",
            Keywords = [Themes[(setSequence - 1) % Themes.Length], EvergreenKeywords[(setSequence - 1) % EvergreenKeywords.Length]],
            CardIds = versions.Select(version => version.CardId).ToList(),
            UnissuedLegendaryIds = versions
                .Where(version => version.Definition.Rarity == CardRarity.Legendary)
                .Select(version => version.CardId)
                .ToHashSet(StringComparer.Ordinal),
            HiddenCollectorLegendaryIds = new HashSet<string>(StringComparer.Ordinal),
        };
    }

    public IReadOnlyList<CardVersion> GenerateVersionsForSet(CardSet set)
    {
        return BuildRecipes(set.Id, set.Sequence).Select(Resolve).ToList();
    }

    private IReadOnlyList<CardGenerationRecipe> BuildRecipes(string setId, int setSequence)
    {
        var recipes = new List<CardGenerationRecipe>(config.Sets.TotalCards);
        var sequence = 1;

        void AddCards(CardType family, CardRarity rarity, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var familyCode = family switch
                {
                    CardType.Champion => "CH",
                    CardType.Unit => "UN",
                    CardType.Spell => "SP",
                    CardType.Counter => "CT",
                    _ => "CD",
                };

                var cardId = $"{setId}-{familyCode}{index + 1:00}-{rarity.ToString()[0]}";
                var roleKey = $"{familyCode}-{rarity}-{index % 5}";
                var baseTemplate = BuildBaseTemplate(family, rarity, index);
                var archetype = BuildArchetype(setSequence, family, index);
                var modifiers = BuildModifiers(family, rarity, setSequence, index);
                recipes.Add(new CardGenerationRecipe(
                    setId,
                    cardId,
                    BuildName(setSequence, family, index),
                    family,
                    rarity,
                    baseTemplate,
                    archetype,
                    modifiers,
                    roleKey,
                    sequence++));
            }
        }

        foreach (var pair in config.Sets.ChampionRarityCounts.OrderBy(pair => pair.Key))
        {
            AddCards(CardType.Champion, pair.Key, pair.Value);
        }

        var remainingByRarity = config.Sets.RarityCounts.ToDictionary(pair => pair.Key, pair => pair.Value - config.Sets.ChampionRarityCounts.GetValueOrDefault(pair.Key));
        AddFamilyByRarity(CardType.Unit, config.Sets.Units, remainingByRarity, recipes, ref sequence, setSequence, setId);
        AddFamilyByRarity(CardType.Spell, config.Sets.Spells, remainingByRarity, recipes, ref sequence, setSequence, setId);
        AddFamilyByRarity(CardType.Counter, config.Sets.Counters, remainingByRarity, recipes, ref sequence, setSequence, setId);

        return recipes.OrderBy(recipe => recipe.Sequence).ToList();
    }

    private static void AddFamilyByRarity(
        CardType family,
        int totalCount,
        IDictionary<CardRarity, int> remainingByRarity,
        List<CardGenerationRecipe> recipes,
        ref int sequence,
        int setSequence,
        string setId)
    {
        var rarityOrder = new[] { CardRarity.Common, CardRarity.Rare, CardRarity.Epic, CardRarity.Legendary };
        var allocated = 0;
        foreach (var rarity in rarityOrder)
        {
            if (allocated >= totalCount)
            {
                break;
            }

            var take = family switch
            {
                CardType.Unit => rarity switch
                {
                    CardRarity.Common => Math.Min(26, remainingByRarity[rarity]),
                    CardRarity.Rare => Math.Min(13, remainingByRarity[rarity]),
                    CardRarity.Epic => Math.Min(7, remainingByRarity[rarity]),
                    CardRarity.Legendary => Math.Min(4, remainingByRarity[rarity]),
                    _ => 0,
                },
                CardType.Spell => rarity switch
                {
                    CardRarity.Common => Math.Min(10, remainingByRarity[rarity]),
                    CardRarity.Rare => Math.Min(6, remainingByRarity[rarity]),
                    CardRarity.Epic => Math.Min(3, remainingByRarity[rarity]),
                    CardRarity.Legendary => Math.Min(1, remainingByRarity[rarity]),
                    _ => 0,
                },
                CardType.Counter => rarity switch
                {
                    CardRarity.Common => Math.Min(7, remainingByRarity[rarity]),
                    CardRarity.Rare => Math.Min(1, remainingByRarity[rarity]),
                    CardRarity.Epic => Math.Min(0, remainingByRarity[rarity]),
                    CardRarity.Legendary => Math.Min(2, remainingByRarity[rarity]),
                    _ => 0,
                },
                _ => 0,
            };

            take = Math.Min(take, totalCount - allocated);
            for (var index = 0; index < take; index++)
            {
                var familyCode = family switch
                {
                    CardType.Unit => "UN",
                    CardType.Spell => "SP",
                    CardType.Counter => "CT",
                    _ => "CD",
                };

                var overallIndex = allocated + index;
                var cardId = $"{setId}-{familyCode}{overallIndex + 1:00}-{rarity.ToString()[0]}";
                var recipe = new CardGenerationRecipe(
                    setId,
                    cardId,
                    BuildName(setSequence, family, overallIndex),
                    family,
                    rarity,
                    BuildBaseTemplate(family, rarity, overallIndex),
                    BuildArchetype(setSequence, family, overallIndex),
                    BuildModifiers(family, rarity, setSequence, overallIndex),
                    $"{familyCode}-{rarity}-{overallIndex % 5}",
                    sequence++);
                recipes.Add(recipe);
            }

            allocated += take;
            remainingByRarity[rarity] -= take;
        }
    }

    private static string BuildName(int setSequence, CardType family, int index)
    {
        var theme = Themes[(setSequence + index) % Themes.Length];
        var noun = family switch
        {
            CardType.Champion => "Champion",
            CardType.Unit => "Unit",
            CardType.Spell => "Spell",
            CardType.Counter => "Counter",
            _ => "Card",
        };

        return $"{theme} {noun} {index + 1}";
    }

    private static BaseTemplate BuildBaseTemplate(CardType family, CardRarity rarity, int index)
    {
        return family switch
        {
            CardType.Champion => new BaseTemplate($"champ-{rarity}-{index % 4}", family, 0, 18 + (int)rarity, 3 + (index % 5), 0, 1, []),
            CardType.Unit => new BaseTemplate($"unit-{rarity}-{index % 4}", family, 1 + (index % 3) + ((int)rarity / 2), 2 + (index % 4), 0, rarity == CardRarity.Common ? 1 : 2, 1, []),
            CardType.Spell => new BaseTemplate($"spell-{rarity}-{index % 4}", family, 0, 0, 0, rarity == CardRarity.Common ? 1 : 2, 1, []),
            CardType.Counter => new BaseTemplate($"counter-{rarity}-{index % 4}", family, 0, 0, 0, rarity == CardRarity.Common ? 1 : 2, 1, []),
            _ => throw new InvalidOperationException(),
        };
    }

    private static ArchetypeTemplate BuildArchetype(int setSequence, CardType family, int index)
    {
        var theme = Themes[(setSequence + index) % Themes.Length];
        return family switch
        {
            CardType.Champion => new ArchetypeTemplate($"champ-arch-{index % 4}", theme, family, 0, index % 2, index % 3, 0, []),
            CardType.Unit => new ArchetypeTemplate($"unit-arch-{index % 4}", theme, family, index % 2, index % 3, 0, 0, index % 5 == 0 ? ["Defender"] : []),
            CardType.Spell => new ArchetypeTemplate($"spell-arch-{index % 4}", theme, family, 0, 0, 0, 1 + (index % 3), []),
            CardType.Counter => new ArchetypeTemplate($"counter-arch-{index % 4}", theme, family, 0, 0, 0, 1, []),
            _ => throw new InvalidOperationException(),
        };
    }

    private static IReadOnlyList<ModifierPass> BuildModifiers(CardType family, CardRarity rarity, int setSequence, int index)
    {
        var modifiers = new List<ModifierPass>();
        if (family == CardType.Unit && index % 7 == 0)
        {
            modifiers.Add(new ModifierPass("defender-pass", 0, 1, 0, 0, 0, ["Defender"], []));
        }
        else if (family == CardType.Unit && index % 7 == 1)
        {
            modifiers.Add(new ModifierPass("magnet-pass", 0, 0, 0, 0, 0, ["Magnet"], []));
        }

        if (family == CardType.Champion && rarity >= CardRarity.Epic)
        {
            modifiers.Add(new ModifierPass("swift-pass", 0, 1, 1, 0, 0, ["Swift"], []));
        }

        if (family == CardType.Spell && setSequence % 2 == 0 && index % 3 == 0)
        {
            modifiers.Add(new ModifierPass("burst-pass", 0, 0, 0, 0, 0, ["Burst"], []));
        }

        return modifiers;
    }

    private CardVersion Resolve(CardGenerationRecipe recipe)
    {
        var keywords = recipe.BaseTemplate.Keywords
            .Concat(recipe.ArchetypeTemplate.PreferredKeywords)
            .Concat(recipe.Modifiers.SelectMany(modifier => modifier.AddedKeywords))
            .Except(recipe.Modifiers.SelectMany(modifier => modifier.RemovedKeywords), StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var attack = Math.Max(0, recipe.BaseTemplate.Attack + recipe.ArchetypeTemplate.AttackDelta + recipe.Modifiers.Sum(modifier => modifier.AttackDelta));
        var health = Math.Max(0, recipe.BaseTemplate.Health + recipe.ArchetypeTemplate.HealthDelta + recipe.Modifiers.Sum(modifier => modifier.HealthDelta));
        var speed = Math.Max(0, recipe.BaseTemplate.Speed + recipe.ArchetypeTemplate.SpeedDelta + recipe.Modifiers.Sum(modifier => modifier.SpeedDelta));
        var power = Math.Max(0, recipe.BaseTemplate.PowerBudget + recipe.Modifiers.Sum(modifier => modifier.PowerDelta));
        var complexity = recipe.BaseTemplate.ComplexityBudget + recipe.Modifiers.Sum(modifier => modifier.ComplexityDelta);
        var validation = Validate(recipe, power, complexity, keywords);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var definition = CreateDefinition(recipe, attack, health, speed, power, keywords);
        return new CardVersion(recipe.CardId, 1, definition, attack, health, speed, power, keywords, definition.RulesText);
    }

    private static ValidationResult Validate(CardGenerationRecipe recipe, int power, int complexity, IReadOnlyList<string> keywords)
    {
        if (power > 6)
        {
            return new ValidationResult(false, $"Card '{recipe.CardId}' exceeds the MVP power budget.");
        }

        if (complexity > 4)
        {
            return new ValidationResult(false, $"Card '{recipe.CardId}' exceeds the MVP complexity budget.");
        }

        if (recipe.Family == CardType.Counter && keywords.Count > 1)
        {
            return new ValidationResult(false, $"Counter '{recipe.CardId}' exceeds the keyword limit.");
        }

        return new ValidationResult(true);
    }

    private static CardDefinition CreateDefinition(CardGenerationRecipe recipe, int attack, int health, int speed, int power, IReadOnlyList<string> keywords)
    {
        var rulesText = BuildRulesText(recipe.Family, recipe.ArchetypeTemplate.EffectValue, keywords);
        return recipe.Family switch
        {
            CardType.Champion => new ChampionCardDefinition(recipe.CardId, recipe.Name, speed, attack, health, keywords.Contains("Swift", StringComparer.Ordinal))
            {
                Rarity = recipe.Rarity,
                SetId = recipe.SetId,
                Version = 1,
                IsUnique = recipe.Rarity == CardRarity.Legendary,
                RulesText = rulesText,
                Keywords = keywords,
                Power = 0,
            },
            CardType.Unit => new UnitCardDefinition(recipe.CardId, recipe.Name, attack, health, keywords.Contains("Defender", StringComparer.Ordinal), keywords.Contains("Magnet", StringComparer.Ordinal))
            {
                Rarity = recipe.Rarity,
                SetId = recipe.SetId,
                Version = 1,
                IsUnique = recipe.Rarity == CardRarity.Legendary,
                RulesText = rulesText,
                Keywords = keywords,
                Power = power == 0 ? 1 : power,
            },
            CardType.Spell => new SpellCardDefinition(recipe.CardId, recipe.Name)
            {
                Rarity = recipe.Rarity,
                SetId = recipe.SetId,
                Version = 1,
                IsUnique = recipe.Rarity == CardRarity.Legendary,
                RulesText = rulesText,
                Keywords = keywords,
                Power = power == 0 ? 1 : power,
                EffectValue = recipe.ArchetypeTemplate.EffectValue,
            },
            CardType.Counter => new CounterCardDefinition(recipe.CardId, recipe.Name, BuildCounterTrigger(recipe.Sequence))
            {
                Rarity = recipe.Rarity,
                SetId = recipe.SetId,
                Version = 1,
                IsUnique = recipe.Rarity == CardRarity.Legendary,
                RulesText = rulesText,
                Keywords = keywords,
                Power = power == 0 ? 1 : power,
                CounterWindow = BuildCounterTrigger(recipe.Sequence),
            },
            _ => throw new InvalidOperationException(),
        };
    }

    private static CounterTriggerType BuildCounterTrigger(int sequence) => (sequence % 4) switch
    {
        0 => CounterTriggerType.EnemySpellWouldResolve,
        1 => CounterTriggerType.EnemyAbilityWouldResolve,
        2 => CounterTriggerType.EnemyCounterWouldResolve,
        _ => CounterTriggerType.EnemyUnitAttacks,
    };

    private static string BuildRulesText(CardType family, int effectValue, IReadOnlyList<string> keywords)
    {
        var keywordText = keywords.Count == 0 ? "No keyword." : string.Join(", ", keywords);
        return family switch
        {
            CardType.Champion => $"Champion. {keywordText}",
            CardType.Unit => $"Unit. {keywordText}",
            CardType.Spell => (effectValue % 3) switch
            {
                0 => $"Deal {Math.Max(1, effectValue)} damage to an enemy unit. {keywordText}",
                1 => $"Give the oldest friendly unit +{Math.Max(1, effectValue)} Attack. {keywordText}",
                _ => $"Heal your Champion for {Math.Max(1, effectValue)}. {keywordText}",
            },
            CardType.Counter => $"Counter window: {keywordText}",
            _ => keywordText,
        };
    }
}
