import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { z } from 'astro/zod';

// One markdown file per Veranstaltung; the file name becomes the URL. Files
// starting with `_` (the copy-me template) are not loaded.
const veranstaltungen = defineCollection({
  loader: glob({ pattern: '**/[^_]*.md', base: './src/content/veranstaltungen' }),
  schema: ({ image }) => {
    // Quoted ISO datetime *with* offset. An unquoted YAML timestamp arrives as
    // a Date and a missing offset would silently mean UTC, so both are rejected.
    const dateTime = z
      .string()
      .regex(
        /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?(Z|[+-]\d{2}:\d{2})$/,
        'Datum in Anführungszeichen mit Zeitzone angeben, z. B. "2026-05-30T09:00:00+02:00"',
      )
      .transform((value) => new Date(value));

    return z
      .object({
        title: z.string().min(1),
        summary: z.string().min(1),
        start: dateTime,
        end: dateTime.optional(),
        cover: z.object({ image: image(), alt: z.string().min(1) }).optional(),
        price: z.string().optional(),
        location: z.string().optional(),
        highlights: z.array(z.string()).default([]),
        bring: z.array(z.string()).default([]),
        contact: z.object({ name: z.string().optional(), phone: z.string().optional() }).optional(),
      })
      .refine((event) => !event.end || event.end > event.start, {
        message: 'end muss nach start liegen',
        path: ['end'],
      });
  },
});

export const collections = { veranstaltungen };
