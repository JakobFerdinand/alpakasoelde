import { afterEach, describe, expect, test, vi } from 'vitest';
import { apiRequest } from '../src/utils/api';
import { pickCase } from '../src/utils/casing';

const json = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });

const stubFetch = (
  implementation: (url: string, init?: RequestInit) => Response | Promise<Response>,
) => {
  const mock = vi.fn(implementation);
  vi.stubGlobal('fetch', mock);
  return mock;
};

const abortError = () =>
  Object.assign(new Error('Der Abruf wurde abgebrochen.'), { name: 'AbortError' });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('apiRequest', () => {
  test('parses the JSON of a successful response and passes the request through', async () => {
    const mock = stubFetch(() => json({ total: 3, paths: ['/'] }));

    const outcome = await apiRequest<{ total: number; paths: string[] }>('/api/pageviews/stats', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question: 'Wieviele Besucher?' }),
    });

    expect(outcome).toEqual({ ok: true, data: { total: 3, paths: ['/'] } });
    expect(mock.mock.calls[0][0]).toBe('/api/pageviews/stats');
    const request = mock.mock.calls[0][1] as RequestInit;
    expect(request.method).toBe('POST');
    expect(request.headers).toEqual({ 'Content-Type': 'application/json' });
  });

  test('carries the problem-details detail of a non-ok response', async () => {
    stubFetch(() => json({ title: 'Ungültig', status: 400, detail: 'Kaufdatum fehlt.' }, 400));

    const outcome = await apiRequest('/api/gutscheine', {
      fallback: 'Gutschein konnte nicht gespeichert werden.',
    });

    expect(outcome).toEqual({
      ok: false,
      kind: 'status',
      status: 400,
      message: 'Kaufdatum fehlt.',
    });
  });

  test('accepts a PascalCase Detail on a non-ok response', async () => {
    stubFetch(() => json({ Title: 'Fehler', Status: 403, Detail: 'Kein Zugriff.' }, 403));

    const outcome = await apiRequest('/api/gutscheine', {
      fallback: 'Gutschein konnte nicht gespeichert werden.',
    });

    expect(outcome).toEqual({
      ok: false,
      kind: 'status',
      status: 403,
      message: 'Kein Zugriff.',
    });
  });

  test('falls back on a non-ok response with an empty body', async () => {
    stubFetch(() => new Response('', { status: 500 }));

    const outcome = await apiRequest('/api/messages', {
      fallback: 'Nachrichten konnten nicht geladen werden.',
    });

    expect(outcome).toEqual({
      ok: false,
      kind: 'status',
      status: 500,
      message: 'Nachrichten konnten nicht geladen werden.',
    });
  });

  test('falls back on a non-ok response with a non-JSON body', async () => {
    stubFetch(() => new Response('<h1>Fehler</h1>', { status: 502 }));

    const outcome = await apiRequest('/api/messages', {
      fallback: 'Nachrichten konnten nicht geladen werden.',
    });

    expect(outcome).toEqual({
      ok: false,
      kind: 'status',
      status: 502,
      message: 'Nachrichten konnten nicht geladen werden.',
    });
  });

  test('does not report a superseded request as an error', async () => {
    const controller = new AbortController();
    controller.abort();
    stubFetch(() => {
      throw abortError();
    });

    const outcome = await apiRequest('/api/pageviews/sessions', { signal: controller.signal });

    expect(outcome).toEqual({ ok: false, kind: 'aborted' });
  });

  test('maps a failing request itself (network error) to the fallback', async () => {
    stubFetch(() => {
      throw TypeError('fetch failed');
    });

    const outcome = await apiRequest('/api/events', {
      fallback: 'Ereignisse konnten nicht geladen werden.',
    });

    expect(outcome).toEqual({
      ok: false,
      kind: 'network',
      message: 'Ereignisse konnten nicht geladen werden.',
    });
  });

  test('maps a non-JSON body of a 200 response to the network fallback', async () => {
    stubFetch(
      () =>
        new Response('<!doctype html><html><body>SWA-Fallback</body></html>', {
          status: 200,
          headers: { 'content-type': 'text/html' },
        }),
    );

    const outcome = await apiRequest('/api/messages', {
      fallback: 'Nachrichten konnten nicht geladen werden.',
    });

    expect(outcome).toEqual({
      ok: false,
      kind: 'network',
      message: 'Nachrichten konnten nicht geladen werden.',
    });
  });
});

describe('pickCase', () => {
  test('reads the value under the camelCase spelling', () => {
    expect(pickCase<number>({ total: 3 }, 'total')).toBe(3);
  });

  test('reads the PascalCase spelling when camelCase is absent', () => {
    expect(pickCase<number>({ Total: 3 }, 'total')).toBe(3);
  });

  test('keeps an explicit null instead of peeking at the other casing', () => {
    expect(pickCase<number | null>({ cost: null, Cost: 5 }, 'cost')).toBeNull();
  });

  test('returns undefined for keys that are nowhere in the payload', () => {
    expect(pickCase<number>({ Spam: 1 }, 'legit')).toBeUndefined();
  });

  test('returns undefined for anything that is not an object', () => {
    expect(pickCase<number>(null, 'total')).toBeUndefined();
    expect(pickCase<number>('2025', 'total')).toBeUndefined();
    expect(pickCase<number>(undefined, 'total')).toBeUndefined();
  });
});
