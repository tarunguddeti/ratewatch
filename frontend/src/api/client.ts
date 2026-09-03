// Thin typed fetch wrapper - the single place that knows the backend's address. Normalizes
// every error, HTTP or network, into one ApiError shape before a component ever sees it.
// The frontend never calls Frankfurter directly, under any condition - this is the only
// HTTP client in the app.

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

const JSON_CONTENT_TYPE = "application/json";

// Named HTTP-status constants this file (and WatchlistDetailPage.tsx's own 404 check) refer to.
const HTTP_NO_CONTENT = 204;
export const HTTP_NOT_FOUND = 404;
const HTTP_SERVER_ERROR_THRESHOLD = 500;

export interface ApiError {
  /** null = the request never reached the backend at all (network-level failure). */
  status: number | null;
  title: string;
  detail?: string;
  fieldErrors?: Record<string, string[]>;
  /** Only present for 5xx - a 4xx is already fixable from the message alone. */
  traceId?: string;
}

function isApiError(value: unknown): value is ApiError {
  return typeof value === "object" && value !== null && "title" in value;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { "Content-Type": JSON_CONTENT_TYPE, ...init?.headers },
    });
  } catch {
    const networkError: ApiError = {
      status: null,
      title: "Can't reach the server",
      detail: "Check your connection and try again.",
    };
    throw networkError;
  }

  if (response.status === HTTP_NO_CONTENT) {
    return undefined as T;
  }

  const body: unknown = await response.json().catch(() => null);

  if (!response.ok) {
    const problem = isApiError(body) ? body : { title: "Something went wrong" };
    const apiError: ApiError = {
      status: response.status,
      title: problem.title ?? "Something went wrong",
      detail: (body as { detail?: string } | null)?.detail,
      fieldErrors: (body as { errors?: Record<string, string[]> } | null)?.errors,
      traceId: response.status >= HTTP_SERVER_ERROR_THRESHOLD ? (body as { traceId?: string } | null)?.traceId : undefined,
    };
    throw apiError;
  }

  return body as T;
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
  delete: (path: string) => request<void>(path, { method: "DELETE" }),
};
