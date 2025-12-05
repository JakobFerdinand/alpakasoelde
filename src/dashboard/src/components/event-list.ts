export type EventListItem = {
  eventType: string;
  eventDate: string;
  alpakaNames?: string[];
  comment?: string;
  cost?: number | null;
};

export type EventListOptions = {
  emptyText?: string;
  showAlpakaNames?: boolean;
};

const formatDate = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return value;
  return date.toLocaleDateString('de-AT', { year: 'numeric', month: 'long', day: 'numeric' });
};

const formatCurrency = (value: number | string | null | undefined) => {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '';
  return new Intl.NumberFormat('de-AT', { style: 'currency', currency: 'EUR' }).format(Number(value));
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

const EVENT_TYPE_ICON_KEYS: Record<string, string> = {
  entwurmen: 'worm',
  'nägel schneiden': 'scissors',
  'naegel schneiden': 'scissors',
  impfen: 'syringe',
  gesundheitscheck: 'stethoscope'
};

const resolveIconKey = (eventType: string | undefined): string => {
  if (!eventType) return 'circle-ellipsis';

  const normalizedType = eventType.trim().toLowerCase();
  return EVENT_TYPE_ICON_KEYS[normalizedType] ?? 'circle-ellipsis';
};

const cloneIconTemplate = (iconKey: string) => {
  const templates = document.getElementById('event-icon-templates');
  if (!templates) return undefined;

  const template = templates.querySelector(`[data-icon="${iconKey}"] svg`);
  return template?.cloneNode(true) as SVGElement | undefined;
};

export const renderEventList = (
  container: HTMLElement | null,
  events: EventListItem[],
  options: EventListOptions = {}
) => {
  if (!container) return;

  container.classList.remove('loading');
  container.innerHTML = '';

  const emptyText = options.emptyText ?? 'Keine Ereignisse vorhanden.';
  const showAlpakaNames = options.showAlpakaNames ?? true;

  if (!events || events.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'empty';
    empty.textContent = emptyText;
    container.appendChild(empty);
    return;
  }

  const list = document.createElement('ul');
  list.className = 'event-list';

  events.forEach((item) => {
    const entry = document.createElement('li');
    entry.className = 'event-item';

    const header = document.createElement('div');
    header.className = 'event-header';

    const titleGroup = document.createElement('div');
    titleGroup.className = 'event-title-group';

    const icon = cloneIconTemplate(resolveIconKey(item.eventType));

    const title = document.createElement('div');
    title.className = 'event-title';
    title.textContent = item.eventType || 'Ereignis';

    if (icon) {
      icon.classList.add('event-icon');
      titleGroup.appendChild(icon);
    }
    titleGroup.appendChild(title);

    const date = document.createElement('div');
    date.className = 'event-date';
    date.textContent = formatDate(item.eventDate);

    header.appendChild(titleGroup);
    header.appendChild(date);
    entry.appendChild(header);

    if (showAlpakaNames && Array.isArray(item.alpakaNames) && item.alpakaNames.length > 0) {
      const chips = document.createElement('div');
      chips.className = 'alpaka-chips';
      item.alpakaNames.forEach((name) => {
        const chip = document.createElement('span');
        chip.className = 'alpaka-chip';
        chip.textContent = name;
        chips.appendChild(chip);
      });
      entry.appendChild(chips);
    }

    if (item.comment) {
      const comment = document.createElement('p');
      comment.className = 'event-comment';
      comment.textContent = item.comment;
      entry.appendChild(comment);
    }

    const formattedCost = formatCurrency(item.cost as any);
    if (formattedCost) {
      const cost = document.createElement('p');
      cost.className = 'event-cost';
      cost.textContent = `Kosten: ${formattedCost}`;
      entry.appendChild(cost);
    }

    list.appendChild(entry);
  });

  container.appendChild(list);
};

export const renderLoadingState = (container: HTMLElement | null, loadingText: string) => {
  if (!container) return;
  container.classList.add('loading');
  container.innerHTML = `<p class="loading-text">${loadingText}</p>`;
};
