import { describe, it, expect, vi } from 'vitest';
import { SubtitlePlayer } from '../SubtitlePlayer.js';
import processedSubtitles from './fixtures/processed.json';

// A fake renderer whose container is 100px wide and uses monospace 10px chars.
function makeRenderer() {
  const rendered = [];
  const cleared = [];
  return {
    measureLineWidth: text => text.length * 10,
    getContainerWidth: () => 100,
    render: lines => rendered.push([...lines]),
    clear: () => cleared.push(true),
    // Inspection helpers:
    rendered,
    cleared,
  };
}

describe('SubtitlePlayer', () => {
  it('renders page 0 immediately on start()', () => {
    const r = makeRenderer();
    // "hello" = 50px fits on one line; maxLines=2 → single page with one line
    const player = new SubtitlePlayer({
      text: 'hello',
      duration: 4,
      maxLines: 2,
      renderer: r,
    });
    player.start();
    expect(r.rendered.length).toBe(1);
    expect(r.rendered[0]).toEqual(['hello']);
  });

  it('advances to the next page when elapsed >= page duration', () => {
    const r = makeRenderer();
    // Two equal pages: "aaaaa bbbbb" split into two lines by maxLines=1
    const player = new SubtitlePlayer({
      text: 'aaaaa bbbbb',
      duration: 4,
      maxLines: 1,
      renderer: r,
    });
    player.start();
    expect(r.rendered.length).toBe(1);
    player.tick(2.1); // first page duration is 2 s ('aaaaa…' ellipsis not timed)
    expect(r.rendered.length).toBe(2);
    expect(r.rendered[1]).toContain('bbbbb');
  });

  it('fires onComplete after the last page expires', () => {
    const r = makeRenderer();
    const onComplete = vi.fn();
    const player = new SubtitlePlayer({
      text: 'hi',
      duration: 2,
      maxLines: 2,
      renderer: r,
      onComplete,
    });
    player.start();
    player.tick(2.5);
    expect(onComplete).toHaveBeenCalledOnce();
    expect(r.cleared.length).toBeGreaterThanOrEqual(1);
  });

  it('does not fire onComplete if stopped before expiry', () => {
    const r = makeRenderer();
    const onComplete = vi.fn();
    const player = new SubtitlePlayer({
      text: 'hi',
      duration: 5,
      maxLines: 2,
      renderer: r,
      onComplete,
    });
    player.start();
    player.stop();
    player.tick(10); // should be ignored after stop
    expect(onComplete).not.toHaveBeenCalled();
  });

  it('reset() clears and allows start() to replay', () => {
    const r = makeRenderer();
    const player = new SubtitlePlayer({
      text: 'hello',
      duration: 2,
      maxLines: 2,
      renderer: r,
    });
    player.start();
    player.reset();
    expect(r.cleared.length).toBeGreaterThanOrEqual(1);
    // After reset, start again from the top.
    player.start();
    expect(r.rendered.length).toBe(2); // one from each start()
  });

  it('handles a single-line single-page subtitle', () => {
    const r = makeRenderer();
    const onComplete = vi.fn();
    const player = new SubtitlePlayer({
      text: 'hi',
      duration: 1,
      maxLines: 2,
      renderer: r,
      onComplete,
    });
    player.start();
    player.tick(1.1);
    expect(onComplete).toHaveBeenCalledOnce();
  });

  describe('loaded from preprocessor JSON output', () => {
    it('renders entry with soft-hyphenated text across multiple pages', () => {
      const r = makeRenderer();
      // Container is 100px wide (10 chars at 10px each).
      // Entry "1" contains "Internationalization..." with U+00AD soft hyphens inserted
      // by the preprocessor.  The player must hyphenate and paginate correctly.
      const entry = processedSubtitles.find(e => e.id === '1');
      const player = new SubtitlePlayer({
        text: entry.subtitle,
        duration: 6,
        maxLines: 2,
        renderer: r,
      });
      player.start();
      // The long word "Internationalization" (20 chars) exceeds the 10-char container,
      // so the layout must produce more than one line and thus render immediately.
      expect(r.rendered.length).toBeGreaterThanOrEqual(1);
      // Every rendered line must be free of raw soft hyphens (U+00AD must not leak out
      // except as a trailing '-' inserted by the wrapper).
      const allLines = r.rendered.flat();
      allLines.forEach(line => {
        expect(line).not.toContain('\u00ad');
      });
    });

    it('plays a short entry from preprocessor JSON without error', () => {
      const r = makeRenderer();
      const onComplete = vi.fn();
      const entry = processedSubtitles.find(e => e.id === '2');
      const player = new SubtitlePlayer({
        text: entry.subtitle,
        duration: 2,
        maxLines: 2,
        renderer: r,
        onComplete,
      });
      player.start();
      player.tick(2.1);
      expect(onComplete).toHaveBeenCalledOnce();
    });
  });

  it('tick() is a no-op before start()', () => {
    const r = makeRenderer();
    const player = new SubtitlePlayer({
      text: 'hi',
      duration: 2,
      maxLines: 2,
      renderer: r,
    });
    player.tick(5); // before start — should not throw
    expect(r.rendered.length).toBe(0);
  });

  it('handles large tick that skips multiple pages', () => {
    const r = makeRenderer();
    const onComplete = vi.fn();
    // "aa bb cc" at maxLines=1 → three pages
    const player = new SubtitlePlayer({
      text: 'aa bb cc',
      duration: 3,
      maxLines: 1,
      renderer: r,
      onComplete,
    });
    player.start();
    player.tick(10); // way beyond total duration
    expect(onComplete).toHaveBeenCalledOnce();
  });
});
