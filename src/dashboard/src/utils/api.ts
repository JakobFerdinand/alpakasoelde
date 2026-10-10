/**
 * Central data access for the dashboard: same-origin API requests, JSON parsing,
 * problem-details error mapping and "newest request wins" cancellation.
 *
 * Requests resolve to an {@link ApiResult} instead of throwing — an abort of a
 * superseded request can therefore never surface as an error.
 */

export type ApiStatusOutcome = {
  ok: false;
  kind: 'status';
  status: number;
  message: string;
};

export type ApiNetworkOutcome = {
  ok: false;
  kind: 'network';
  message: string;
};

export type ApiAbortedOutcome = {
  ok: false;
  kind: 'aborted';
};

export type ApiResult<T> =
  { ok: true; data: T } | ApiStatusOutcome | ApiNetworkOutcome | ApiAbortedOutcome;

/** Problem-details payload (JSON casing varies: `detail` / `Detail`). */
type ProblemDetails = {
  detail?: string;
  Detail?: string;
};

export type ApiRequestOptions = RequestInit & {
  /** German fallback message for non-ok responses and requests without response. */
  fallback?: string;
  /** German fallback message when the request itself fails (no response). */
  networkFallback?: string;
};

const defaultStatusFallback = (status: number): string =>
  `Die Anfrage ist fehlgeschlagen (Status ${status}).`;

const DEFAULT_NETWORK_FALLBACK = 'Der Server ist momentan nicht erreichbar.';

/**
 * Reads the human-readable message from a problem-details body, trying `detail`
 * and `Detail`, falling back to `fallback` (or a German default).
 */
export const problemMessage = (body: unknown, status: number, fallback?: string): string => {
  const details = typeof body === 'object' && body !== null ? (body as ProblemDetails) : null;
  return details?.detail || details?.Detail || fallback || defaultStatusFallback(status);
};

const isAbortError = (error: unknown): boolean =>
  (error as { name?: string } | null)?.name === 'AbortError';

/**
 * Performs one same-origin API request and resolves to an {@link ApiResult}:
 * non-ok responses are mapped to an error carrying the problem-details `detail`
 * (or `Detail`, or the given German fallback), and aborts are reported as
 * `{ ok: false, kind: 'aborted' }` so they never surface as an error.
 */
export const apiRequest = async <T>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<ApiResult<T>> => {
  let response: Response;
  try {
    response = await fetch(path, options);
  } catch (error) {
    if (isAbortError(error) || options.signal?.aborted) return { ok: false, kind: 'aborted' };
    return {
      ok: false,
      kind: 'network',
      message: options.networkFallback ?? options.fallback ?? DEFAULT_NETWORK_FALLBACK,
    };
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    return {
      ok: false,
      kind: 'status',
      status: response.status,
      message: problemMessage(problem, response.status, options.fallback),
    };
  }

  const text = await response.text().catch(() => '');
  if (!text) return { ok: true, data: undefined as T };
  try {
    return { ok: true, data: JSON.parse(text) as T };
  } catch {
    return {
      ok: false,
      kind: 'network',
      message: options.networkFallback ?? options.fallback ?? DEFAULT_NETWORK_FALLBACK,
    };
  }
};

/**
 * Serialises requests per component: starting a new one aborts the superseded
 * request, so only the newest request is ever "current".
 */
export class RequestGate {
  private controller: AbortController | null = null;

  /** Aborts the ongoing request and returns the signal for the next one. */
  start(): AbortSignal {
    this.controller?.abort();
    this.controller = new AbortController();
    return this.controller.signal;
  }

  /** Whether `signal` still belongs to the newest request of this gate. */
  isCurrent(signal: AbortSignal | null | undefined): boolean {
    return signal !== undefined && this.controller?.signal === signal && !signal.aborted;
  }

  /** Aborts any ongoing request, e.g. when a component unmounts. */
  dispose(): void {
    this.controller?.abort();
    this.controller = null;
  }
}
