import { describe, it, expect, vi } from 'vitest';
import { SubtitlePlayer } from '../SubtitlePlayer.js';

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
    player.tick(2.1); // first page duration is 2s
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
