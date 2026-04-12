# Tarn Engine Architecture

## Proposed architecture

- `Tarn.Domain/Cards.cs`
- `Tarn.Domain/GameState.cs`
- `Tarn.Domain/Engine.cs`
- `Tarn.Domain/League.cs`
- `Tarn.Domain/Fixtures.cs`
- `Tarn.Client/Program.cs`
- `Tarn.Domain.Tests/GameEngineTests.cs`

## Key classes and modules

- `DeckDefinition`
  - Enforces the fixed 30-card deck shape, 3-copy limit, and permanent 100 power cap.
- `CardDefinition` plus `ChampionDefinition`, `UnitDefinition`, `SpellDefinition`, `CounterDefinition`
  - Content-ready card model without shipping actual Tarn content yet.
- `CombatCardState` and `CounterState`
  - Runtime state for board persistence, counters, and deterministic play order.
- `GameState`
  - Serialization-friendly snapshot for simulation, replay, and CLI log output.
- `GameEngine`
  - Explicit round-step engine, combat, fatigue, overtime, and best-of-three flow.
- `MatchResult`, `StandingsEntry`, `SeasonDefinition`
  - League, standings, playoff, and seasonal update scaffolding.

## Event flow design

- `CardPlayed`
- `CardResolved`
- `UnitEnteredPlay`
- `ChampionDamaged`
- `UnitDestroyed`
- `RoundEnded`

Each event gets a monotonically increasing sequence number. Counters only trigger from later events than the sequence where they were set, which enforces the locked future-event rule and keeps replay output auditable.

## Resolution pipeline

1. Play Step
2. Quick Step
3. Resolution Step
4. Attack Step
5. End Step
6. Destruction Step
7. Last Wish Step
8. Win Check

## State model

- Persistent board state lives on `PlayerState.Board`.
- Counters persist on `PlayerState.Counters`.
- Champion life totals live on `PlayerState.Champion`.
- Deterministic ordering comes from `PriorityPlayerId`, `PlayOrder`, and `Sequence`.
- Replay/debug history is captured in `GameLogEntry` and `ReplayEvent`.

## Sample pseudocode for a full round

```text
function PlayRound(state):
  increment round
  reset temporary round modifiers

  for each player:
    if deck empty: apply fatigue
    else: draw top card and emit CardPlayed

  resolve Quick cards
  resolve non-Quick cards
  resolve auto-attacks
  resolve Regen and round-end triggers
  destroy units at 0 or less health
  resolve Last Wish in play order
  run win check and overtime reset if needed
```

## Sample pseudocode for counter chaining

```text
function RaiseEvent(event):
  log event
  collect counters with matching trigger and setSequence < event.sequence
  resolve counters in play order
  if counter effects emit new events:
    RaiseEvent(newEvent)
```

## Sample pseudocode for fatigue and overtime

```text
function ApplyFatigue(player):
  player.fatigueCount += 1
  deal that much damage to champion through Defender rules

function ResetForOvertime(state):
  clear boards and counters
  restore champion health
  reset fatigue to 0
  reshuffle both decks deterministically
```

## Suggested test plan for locked rules

- Champions start in play but are not considered played.
- Quick resolves before non-Quick.
- Units played this round do not attack.
- Defender redirects Champion damage in play order.
- Taunt controls automatic Unit targeting.
- Counters only trigger on future events and can chain.
- Multiple destroyed Units leave simultaneously, then Last Wish resolves in play order.
- Fatigue increments per instance and resets in Overtime.
- Equal-health double lethal starts a fresh Overtime game.
- Match points, standings rank, playoff top 8, and promotion/relegation flags update correctly.
