import { describe, it, expect, vi } from 'vitest';
import { SubtitlePlayer, estimateDuration } from '../SubtitlePlayer.js';

// The same table is pinned in the Unity (EstimateDurationTests.cs), Unreal
// (GameSubtitleEstimateDurationTest.cpp), and Godot (tests/run_tests.gd) players.
// Keep all four in step.
const LINE = 'Get down, now! They have seen us.'; // 33 characters
const CASES = [
  // [label, text, options, expected seconds]
  ['empty text clamps to the floor',        '',                                                  {},                    1.5],
  ['one-word bark clamps to the floor',     'No!',                                               {},                    1.5],
  ['plain line is chars / 14',              LINE,                                                {},                    33 / 14],
  ['soft hyphens are not counted',          'Ex­tra­or­di­nary, isn’t it?', {},              24 / 14],
  ['CJK counts characters, not words',      '今日はとても良い天気ですね。散歩に行きましょうか。',        {},                    25 / 14],
  ['astral character counts once',          '\u{20BB7}野家で牛丼を食べました。',                        { charsPerSecond: 4 }, 13 / 4],
  ['long text clamps to the ceiling',       'x'.repeat(300),                                     {},                    18],
  ['minSeconds override',                   'Hi',                                                { minSeconds: 0.5 },   0.5],
  ['maxSeconds override',                   LINE,                                                { maxSeconds: 2 },     2],
  ['non-positive rate falls back to 14',    LINE,                                                { charsPerSecond: 0 }, 33 / 14],
];

describe('estimateDuration', () => {
  it.each(CASES)('%s', (_label, text, opts, expected) => {
    expect(estimateDuration(text, opts)).toBeCloseTo(expected, 4);
  });

  it('treats null and undefined text as empty', () => {
    expect(estimateDuration(null)).toBe(1.5);
    expect(estimateDuration(undefined)).toBe(1.5);
  });
});

describe('SubtitlePlayer.start without a duration', () => {
  function makeRenderer() {
    return {
      measureLineWidth: text => text.length * 10,
      getContainerWidth: () => 1000,
      render: () => {},
      clear: () => {},
    };
  }

  // LINE estimates to 33 / 14 ≈ 2.357 s.
  it.each([
    ['missing', undefined],
    ['zero', 0],
    ['negative', -1],
    ['NaN', NaN],
  ])('uses the estimate when duration is %s', (_label, duration) => {
    const onComplete = vi.fn();
    const player = new SubtitlePlayer({ maxLines: 2, renderer: makeRenderer() });
    player.start({ text: LINE, duration, onComplete });
    player.tick(2.3);
    expect(onComplete).not.toHaveBeenCalled();
    player.tick(0.1);
    expect(onComplete).toHaveBeenCalledOnce();
  });
});
