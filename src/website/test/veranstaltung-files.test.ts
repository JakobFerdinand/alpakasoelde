import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { expect, test } from 'vitest';
import { pastEventPaths } from '../src/data/veranstaltung-files';

const folder = (files: Record<string, string>) => {
  const dir = mkdtempSync(join(tmpdir(), 'veranstaltungen-'));
  for (const [name, content] of Object.entries(files)) writeFileSync(join(dir, name), content);
  return dir;
};

const event = (start: string, end?: string) =>
  `---\ntitle: "x"\nsummary: "y"\nstart: "${start}"\n${end ? `end: "${end}"\n` : ''}---\nText\n`;

test('only ended events are reported, by the URL of their detail page', () => {
  const dir = folder({
    'alt.md': event('2026-05-30T09:00:00+02:00', '2026-05-30T12:00:00+02:00'),
    'heute-ohne-ende.md': event('2026-06-10T09:00:00+02:00'),
    'gestern-ohne-ende.md': event('2026-06-09T09:00:00+02:00'),
    'bald.md': event('2026-07-01T09:00:00+02:00', '2026-07-01T12:00:00+02:00'),
  });

  expect(pastEventPaths(dir, new Date('2026-06-10T12:00:00+02:00')).sort()).toEqual([
    '/veranstaltungen/alt/',
    '/veranstaltungen/gestern-ohne-ende/',
  ]);
});

test('the template and non-markdown files are ignored', () => {
  const dir = folder({
    '_vorlage.md': event('2020-01-01T09:00:00+01:00'),
    'notizen.txt': 'x',
  });

  expect(pastEventPaths(dir, new Date('2026-06-10T12:00:00+02:00'))).toEqual([]);
});
