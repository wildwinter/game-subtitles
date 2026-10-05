// Compiled (not run) by `npm test`, to check src/index.d.ts under strict mode. The lines
// marked as expected errors below must stay errors, or tsc fails.
import {
  SubtitlePlayer, DomRenderer, CanvasRenderer, CHARACTER_NAME_CLASS,
  estimateDuration, wrapAndPaginate, allocateTimings,
  type SubtitleRenderer, type CharacterContext, type Page,
} from '../src/index.js';

const seconds: number = estimateDuration('Hello', { charsPerSecond: 12, maxSeconds: 10 });
estimateDuration(null);

// @ts-expect-error unknown option
estimateDuration('Hello', { rate: 12 });

const dom = new DomRenderer(document.createElement('div'));
dom.invalidateFont();
const canvas = new CanvasRenderer(document.createElement('canvas'), '16px Arial', 22, 'bold 18px Arial');

const player = new SubtitlePlayer({ renderer: dom, maxLines: 2 });
player.start({ text: 'Hello', onComplete: () => player.stop(), characterName: 'Tam', characterNameColor: '#f0cc88' });
player.start({ text: 'Hello', duration: 3, lineColor: null });
player.tick(1 / 60);
player.maxLines = 3;
const pages: number = player.pageCount;
// @ts-expect-error pageCount is read-only
player.pageCount = 2;
// @ts-expect-error text is required
player.start({ duration: 3 });

// Any object with the four methods is a renderer.
const custom: SubtitleRenderer = {
  measureLineWidth: (text: string) => text.length * 10,
  getContainerWidth: () => 400,
  render: (lines: Page, ctx?: CharacterContext | null) => { void lines; void ctx?.name; },
  clear: () => {},
};
new SubtitlePlayer({ renderer: custom });
new SubtitlePlayer({ renderer: canvas });
// @ts-expect-error a renderer needs all four methods
new SubtitlePlayer({ renderer: { clear: () => {} } });

const laidOut: Page[] = wrapAndPaginate('Hello world', t => t.length * 10, 100, 2);
const timings: number[] = allocateTimings(laidOut, seconds);
const cls: 'gs-character-name' = CHARACTER_NAME_CLASS;
void [pages, timings, cls];
