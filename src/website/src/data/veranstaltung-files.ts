// Build-time only (Node): the sitemap filter in astro.config.mjs runs before
// the content layer exists, so it reads the event files itself. It only needs
// `start` and `end`, which the collection schema already forces to be quoted
// ISO strings.
import { readdirSync, readFileSync } from 'node:fs';
import { join, parse } from 'node:path';
import { isUpcoming } from './veranstaltung';

const field = (frontmatter: string, name: string) => {
  const value = frontmatter.match(
    new RegExp(`^${name}:\\s*["']?([^"'\\r\\n]+?)["']?\\s*$`, 'm'),
  )?.[1];
  return value ? new Date(value) : undefined;
};

// Mirrors the collection loader: markdown only, no `_` prefix.
export const pastEventPaths = (dir: string, now: Date): string[] =>
  readdirSync(dir)
    .filter((file) => file.endsWith('.md') && !file.startsWith('_'))
    .flatMap((file) => {
      const frontmatter =
        readFileSync(join(dir, file), 'utf8').match(/^---\r?\n([\s\S]*?)\r?\n---/)?.[1] ?? '';
      const start = field(frontmatter, 'start');
      if (!start || isUpcoming({ start, end: field(frontmatter, 'end') }, now)) return [];
      return [`/veranstaltungen/${parse(file).name}/`];
    });
