# Changelog

Notable changes to Game Subtitles. Each version is published as a [GitHub Release](https://github.com/wildwinter/game-subtitles/releases) with the same notes.

## [Unreleased]

### Added

- Tests for the Unity and Unreal players' layout, timing, and playback, with the same cases as the JS and Godot players. Before, they only tested the duration estimate.
- `npm run check:unity` and `npm run check:unreal`, which run those tests from the command line in each engine that is installed, and skip when none is. `npm run release` runs both.

### Changed

- Unreal: the automation tests are now in their own Editor module, `GameSubtitlesTests`, so they are never packaged into a game.
- The Unity and Unreal demo setup instructions no longer say to copy the package or plugin into the demo projects, which already load them from the repository.

## [0.3.0] - 2026-10-04

### Added

- A Godot 4 player: a GDScript addon with `GameSubtitlePlayer`, a ready-made `GameSubtitleWidget` renderer built from `Label` nodes, and the same layout, timing, and duration estimate as the other players. It ships as `game-subtitles-godot-v{version}.zip`. `players/player-godot` is also a demo project, and its tests run headlessly with `npm test`.

## [0.2.0] - 2026-10-03

### Added

- A duration estimate on every player: `estimateDuration` (JS), `SubtitlePlayer.EstimateDuration` (Unity), and `UGameSubtitlePlayer::EstimateDuration` (Unreal, also callable from Blueprint). It counts characters, not words, so it also works for languages written without spaces, at 14 characters per second, clamped to 1.5 to 18 seconds. The rate, minimum, and maximum can all be overridden.
- The JS and Unity players now estimate a duration when `start` is called without one, or with zero or less, as the Unreal player already did. The duration is now optional on all three.
- Tests for the Unity and Unreal players.

### Changed

- Unreal: when no duration is given, the estimate is no longer rounded to whole seconds and the minimum is 1.5 seconds instead of 3, so short lines clear sooner.
- The macOS preprocessor binary is now notarised as well as signed, and releases are made with `npm run release`.

## [0.1.3] - 2026-07-15

- Fix to support changes to Unreal 5.8.

## [0.1.2] - 2026-05-27

- Minor tweaks to Unreal API.

## [0.1.1] - 2026-05-21

- Minor tweak to deployed package shape.

## [0.1.0] - 2026-05-20

- Change how the character name font is supplied: it is no longer called the "bold font".
- Tidy up demos.

## [0.0.12] - 2026-05-18

- Tweaks to Unreal rendering to stop a flash of text between lines.
- If your subtitle is supposed to be three lines tall, blank lines are filled in to keep the correct height.

## [0.0.11] - 2026-05-18

- Fix Unreal widget to measure and centre properly.

## [0.0.10] - 2026-05-04

- Add support for soft-hyphen breakpoint insertion into compiled Ink files.

## [0.0.9] - 2026-04-16

- Add `lineColor` as an option.
- Rename all colours to colors.

## [0.0.8] - 2026-04-09

- Added support for a coloured and bolded character name at the start of every subtitle.

## [0.0.7] - 2026-04-09

- Behaviour change: at the end of the subtitle the text is not cleared. The calling code needs to call `Stop()` to clear it.

## [0.0.6] - 2026-04-01

- Use simple-vc-lib to work better with version control setups, and skip rewriting files if there is no content change.

## [0.0.5] - 2026-03-31

- Add simple-vc-lib to work better with version control.
- Add code signing for Apple.

## [0.0.4] - 2026-03-26

- Rejigged distribution into different zips per development platform, and fixed some versioning.

## [0.0.3] - 2026-03-14

- Update code signing info.

## [0.0.2] - 2026-03-12

- Add Apple code signing.

## [0.0.1] - 2026-03-09

- First release.
