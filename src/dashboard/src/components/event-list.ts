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

  const table = document.createElement('table');
  table.className = 'event-table';

  const hasCosts = events.some((event) => !Number.isNaN(Number(event.cost)));
  const totalCost = events.reduce((sum, event) => {
    const value = Number(event.cost);
    if (Number.isNaN(value)) return sum;
    return sum + value;
  }, 0);

  const header = document.createElement('thead');
  const headerRow = document.createElement('tr');

  const dateHeader = document.createElement('th');
  dateHeader.scope = 'col';
  dateHeader.setAttribute('aria-label', 'Datum');
  dateHeader.className = 'event-date-header';
  headerRow.appendChild(dateHeader);

  const typeHeader = document.createElement('th');
  typeHeader.scope = 'col';
  typeHeader.className = 'event-type-header';
  typeHeader.setAttribute('aria-label', 'Ereignistyp');
  headerRow.appendChild(typeHeader);

  const detailHeader = document.createElement('th');
  detailHeader.scope = 'col';
  detailHeader.textContent = 'Details';
  headerRow.appendChild(detailHeader);

  const costHeader = document.createElement('th');
  costHeader.scope = 'col';
  costHeader.className = 'event-cost-header';
  costHeader.textContent = 'Kosten';
  headerRow.appendChild(costHeader);

  header.appendChild(headerRow);
  table.appendChild(header);

  const body = document.createElement('tbody');

  events.forEach((item) => {
    const row = document.createElement('tr');

    const dateCell = document.createElement('td');
    dateCell.className = 'event-date';
    dateCell.textContent = formatDate(item.eventDate);
    row.appendChild(dateCell);

    const typeCell = document.createElement('td');
    typeCell.className = 'event-type';
    const iconWrapper = document.createElement('span');
    iconWrapper.className = 'event-icon-wrapper';
    iconWrapper.title = item.eventType || 'Ereignis';
    iconWrapper.setAttribute('aria-label', item.eventType || 'Ereignis');
    const icon = cloneIconTemplate(resolveIconKey(item.eventType));
    if (icon) {
      icon.classList.add('event-icon');
      icon.setAttribute('aria-hidden', 'true');
      iconWrapper.appendChild(icon);
    }
    typeCell.appendChild(iconWrapper);
    row.appendChild(typeCell);

    const detailCell = document.createElement('td');
    detailCell.className = 'event-details';

    if (showAlpakaNames && Array.isArray(item.alpakaNames) && item.alpakaNames.length > 0) {
      const chips = document.createElement('div');
      chips.className = 'alpaka-chips';
      item.alpakaNames.forEach((name) => {
        const chip = document.createElement('span');
        chip.className = 'alpaka-chip';
        chip.textContent = name;
        chips.appendChild(chip);
      });
      detailCell.appendChild(chips);
    }

    if (item.comment) {
      const comment = document.createElement('p');
      comment.className = 'event-comment';
      comment.textContent = item.comment;
      detailCell.appendChild(comment);
    }

    row.appendChild(detailCell);

    const costCell = document.createElement('td');
    costCell.className = 'event-cost';
    const formattedCost = formatCurrency(item.cost as any);
    costCell.textContent = formattedCost || '—';
    row.appendChild(costCell);

    body.appendChild(row);
  });

  table.appendChild(body);

  if (hasCosts) {
    const footer = document.createElement('tfoot');
    const footerRow = document.createElement('tr');

    const label = document.createElement('td');
    label.className = 'event-total-label';
    label.colSpan = 3;
    label.textContent = 'Summe';
    footerRow.appendChild(label);

    const total = document.createElement('td');
    total.className = 'event-cost event-total';
    total.textContent = formatCurrency(totalCost);
    footerRow.appendChild(total);

    footer.appendChild(footerRow);
    table.appendChild(footer);
  }

  const wrapper = document.createElement('div');
  wrapper.className = 'event-table-wrapper';
  wrapper.appendChild(table);

  container.appendChild(wrapper);
};

export const renderLoadingState = (container: HTMLElement | null, loadingText: string) => {
  if (!container) return;
  container.classList.add('loading');
  container.innerHTML = `<p class="loading-text">${loadingText}</p>`;
};
