# Handover: "Two Rooms" — Asymmetric Co-op Multiplayer (Blazor + SignalR)

## Concept

A browser-based, two-player **asymmetric co-op** game. Both players join the same session but see **different screens** with different information. Neither screen alone is enough to solve the puzzle — players must communicate (voice, chat, or in person) to combine what they each see and progress.

**Stack:** Blazor WebAssembly (client, pure C#) + ASP.NET Core SignalR (server, real-time sync). No Unity, no paid services.

**Core architectural trick:** The server holds the full puzzle state and is authoritative. Each client only ever receives a *filtered* slice of that state — the part their role is allowed to see. This is what creates the asymmetry and prevents cheating (no client ever has the full picture, even if someone reads the browser's network traffic).

**Why this genre fits Blazor well:**
- No physics or 60fps render loop needed — state changes in discrete events (a guess, a move, a submission)
- SignalR broadcasts fit naturally: "player did X" → server validates → server pushes filtered updates to both roles
- UI-heavy, component-driven — Blazor's Razor components map cleanly to "screens" per role

---

## Replayability — the design problem to solve first

A single hand-authored puzzle is a one-and-done experience. To make any of these genuinely replayable, pick **at least one** of these mechanisms per game, ideally combined:

1. **Procedural generation with a seed** — generate the maze/wiring/rune sequence/etc. from a random seed each session. Same session code = same seed = reproducible (good for speedrunning/sharing).
2. **Puzzle pools** — author 15–30 small puzzle variants per room type, shuffle which ones appear and in what order.
3. **Timer / scoring** — track completion time and mistakes, show a leaderboard (even a local one at first). Turns "solve once" into "beat your best."
4. **Role swap** — after finishing, swap who sees what and play the same puzzle type again from the other side. Doubles replay value for free, and is a natural onboarding step for new players.
5. **Difficulty/modifier settings** — larger maze, more runes, less time, "no repeating words" rule, etc. — cheap to add, extends life a lot.

Recommendation: build **seeded procedural generation + timer/scoring** first (mechanisms 1 & 3) since they're cheap and apply to every game below; add role swap once the core loop works; treat puzzle pools and modifiers as post-launch content updates.

---

## Game Ideas, Ordered by Ease of Implementation

### 1. Reaction/Sync Duel (warm-up project — build this first)
Not a "Two Rooms" puzzle exactly, but the right first build to prove your SignalR pipeline works before committing to anything asymmetric. Both players see a shared signal; first to react correctly scores a point, best of N rounds.
- **Replayability:** randomized signal timing/type each round, best-of-N match structure, running win/loss record between the same two players.
- **Effort:** ~1 weekend. Pure state broadcast, no per-role filtering needed yet.

### 2. Maze Navigator
Player A sees the maze layout (walls only, no marker for B's position). Player B sees only their current position/surroundings and can move (up/down/left/right), but not the maze layout. A guides B verbally to the exit.
- **Replayability:** procedurally generate a new maze from a seed every session; track time-to-exit; add a "fog of war" difficulty mode where A only sees walls near B's last known position.
- **Effort:** Small. One grid data structure, one "reveal filter" (what B currently sees vs. what A sees), straightforward move validation.

### 3. Symbol-Matching Lock
Player A sees a sequence of runes/symbols shown on a door. Player B has a "codex" mapping symbols to numbers/letters/actions. Together they work out and enter the correct code.
- **Replayability:** randomize both the symbol set and the codex mapping per session (from a shared seed); add multi-stage locks (solve 3 codes to open the door) with escalating symbol-set size.
- **Effort:** Small–medium. Mostly data-driven (symbol pool + mapping table), UI is a grid of symbols plus a numeric/text input.

### 4. Bomb-Defusal Style
Player A sees a wiring diagram with colored/symbol-coded wires. Player B has a manual explaining which wire to cut based on conditions (e.g., "if there are 2 red wires, cut the last one"). Time pressure adds tension.
- **Replayability:** procedurally generate wire layouts and manual rules from a seed each session (this is literally how *Keep Talking and Nobody Explodes* stays replayable — rule-based generation, not hand-authored puzzles); add a visible countdown timer for stakes.
- **Effort:** Medium. Needs a small rule-engine (conditional logic evaluated server-side) rather than a fixed answer table, so it's a step up from #2/#3, but this is also what gives it the most long-term replay value of the puzzle-style games.

### 5. Room Description Puzzle
Player A is "outside," describing objects visible through a window (shapes, colors, positions). Player B is "inside" and must drag-and-drop tiles to arrange objects matching A's description.
- **Replayability:** randomize object sets, positions, and count each round; add a "no using color names" or "no using position words" constraint mode for harder replays with the same friend group.
- **Effort:** Medium. Needs drag-and-drop UI (more front-end work than the others) plus a matching/validation check comparing B's arrangement to A's ground truth.

### 6. Multi-Room Campaign (combine the above)
Chain several of the room types above into a single session — players move from a maze, to a lock, to a wire-cut room, escalating in difficulty, framed as "escaping" together.
- **Replayability:** shuffle which room types appear and in what order each playthrough (using the pooled puzzles + seeds from each individual game above); add a global timer across the whole run for a speedrun leaderboard.
- **Effort:** Large — this is really a packaging/orchestration layer on top of games 2–5, so it's naturally the last thing to build, once each room type works standalone.

---

## Suggested Build Order

1. Reaction Duel — prove the SignalR + role-based session pipeline works end to end.
2. Maze Navigator — first real "Two Rooms" asymmetric game, simplest state-filtering logic.
3. Symbol-Matching Lock — reuse the session/role infrastructure from #2, add a small rule/mapping layer.
4. Bomb-Defusal — introduces a proper generated rule-engine; biggest replayability payoff.
5. Room Description Puzzle — biggest front-end lift (drag-and-drop), do this once the backend patterns are solid.
6. Multi-Room Campaign — combine everything into one polished portfolio piece.

## Shared Architecture Notes (applies to all games above)

- **ASP.NET Core + SignalR Hub** (`GameHub.cs`):
  - `JoinSession(sessionCode, requestedRole)` — pairs two players, assigns roles
  - `SubmitAction(sessionCode, action)` — validated server-side against the true puzzle state
  - Server pushes **role-filtered** state updates after every validated action, never the full state
- **Session codes** — short alphanumeric codes, no login required, shareable with a friend
- **Seeded RNG** — store the seed with the session so a session code can be replayed identically (good for "beat this exact maze" challenges/sharing)
- **Hosting (free):** Blazor WASM client → GitHub Pages; SignalR server → Render or Fly.io free tier
