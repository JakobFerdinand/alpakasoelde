export type EventListItem = {
  eventType: string;
  eventDate: string;
  alpakaNames?: string[];
  comment?: string;
  cost?: number | null;
};

export const normalizeEvents = (events: unknown): EventListItem[] => {
  if (!Array.isArray(events)) return [];

  return events.map((event) => ({
    eventType: (event as any).eventType ?? (event as any).EventType ?? '',
    eventDate: (event as any).eventDate ?? (event as any).EventDate ?? '',
    alpakaNames: (event as any).alpakaNames ?? (event as any).AlpakaNames ?? [],
    comment: (event as any).comment ?? (event as any).Comment ?? '',
    cost: (event as any).cost ?? (event as any).Cost ?? null
  }));
};

export const EVENT_TYPE_ICON_KEYS: Record<string, string> = {
  entwurmen: 'worm',
  'nägel schneiden': 'scissors',
  'naegel schneiden': 'scissors',
  impfen: 'syringe',
  gesundheitscheck: 'stethoscope'
};
