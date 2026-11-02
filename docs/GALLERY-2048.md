# 2048 gallery page

The visual/interaction reference is [egoist/2048](https://github.com/egoist/2048),
the MyGo-native rendition of the classic game. The cream/brown palette, 500-unit
board, tile colors, score cards, movement/merge/score animations and end messages
are reproduced with native Avalonia controls and MySharp declarations. The C#
game model, persistence, controls and tests are independently implemented; no Go
source, web bundle or image assets are vendored.

```sh
dotnet run -- --page=2048
# Disable storage, or select an isolated save file:
dotnet run -- --page=2048 --no-game-save
dotnet run -- --page=2048 --game-save=/tmp/mysharp-2048.json
# Show state changes immediately, without slide/pop/message delays:
dotnet run -- --page=2048 --reduced-motion
```

The sidebar also includes **2048**. Leaving the page disposes its native view and
animations while retaining the game session; returning resumes the same board.
The game keeps the reference's classic light palette even in a dark gallery shell.
A Viewbox fits its layout to the available gallery area, including 960×680 windows.

## Interaction

- Arrow keys, WASD and HJKL move the tiles.
- A pointer drag across the board swipes once. Escape, resizing or leaving the page
  cancels an in-flight gesture and releases pointer capture.
- Wheel/touchpad deltas over the board accumulate into one direction, with a 180ms
  rest before accepting another gesture. Avalonia's public wheel event has no
  precise-scroll flag: this implementation intentionally accepts mouse-wheel
  gestures too, rather than guessing which device generated the event.
- R, Ctrl+N, Command+N and **New Game** restart while keeping the best score.
- A new 2048 tile pauses play and offers **Keep going** or **Try again**. A board
  without moves offers restart. Dismissing an end message restores board focus.

The normal desktop entry point stores a versioned JSON file at
`LocalApplicationData/MySharp/Gallery/2048.json`. It writes through a temporary
file and renames it over the previous save. Invalid state starts a new game;
valid best scores survive restarts, and game-over saves keep best score rather
than resuming a lost board. Programmatic `Workspace` instances do not persist
unless explicitly passed a save path, so tests do not touch user state.

## Rendering

The page uses code-only MySharp layout, `cx.Scope(tile.Id, ...)` for dynamic tile
identity, native transform transitions for 100ms slides and finite animations for
spawn/merge pops, rising score and the delayed end-message fade. Merged-away tiles
retain their IDs and slide beneath the new tile. Animation frames do not rebuild
the MySharp declarations. Animated resources cancel when the view unmounts.

`--reduced-motion` is an explicit override for this gallery example. It is not a
claim of automatic operating-system motion-preference detection. Game labels and
board help are exposed through native automation properties; platform screen
reader announcement behavior still needs native accessibility verification.

## Verification

```sh
dotnet run --project tests/MySharp.Tests
python3 scripts/game2048-smoke.py
python3 scripts/game2048-motion.py # after building the Gallery
```

The framework suite includes rule/identity/score cases for every direction, blocked
moves, wins/continuation/loss, save roundtrip/corruption, all keyboard aliases,
scaled pointer dragging, gesture momentum/cancellation, minimum layout, navigation
retention, reduced motion and native slide progression without declaration rebuilds.

The native X11 smoke uses isolated local saves. It checks a known merge and score,
restart persistence, a real drag after window resize, and keyboard activation of
win/continue and loss/restart controls. Generated screenshots, temporary saves and
reports stay in ignored `artifacts/2048/`. Windows/macOS input, touch hardware and
screen reader behavior have not been verified by the X11 run.

Motion follows the local MyGo 2048 implementation: 100 ms cubic ease-in/out slides,
a 100 ms appearance delay, a 200 ms eased scale/opacity ramp for new tiles, and
a 200 ms merge pop (0 → 1.2 → 1) with easing applied independently to each half.
These finite native animations do not rebuild declarations on animation frames.

The motion probe captures 180 native X11 frames and checks intermediate merge
growth plus visible overshoot (about 1.2× the settled width). Its bounded recording
and GIF stay under ignored `artifacts/2048-motion/`. Transform animations target
the visual owner, and tile controls allow the merge to draw beyond their bounds.
