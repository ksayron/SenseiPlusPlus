import { request } from "../api";
import type { PageEnvelope } from "../types";
import type { Mutation, Session } from "./contracts";
export const read = <T>(path: string) =>
  request<T>(`/api/v1/${path}`, undefined, true);
export const send = <T>(
  path: string,
  body: unknown,
  version?: string,
  method = "POST",
) =>
  request<T>(
    `/api/v1/${path}`,
    {
      method,
      body: JSON.stringify(body),
      headers: version ? { "If-Match": `"${version}"` } : undefined,
    },
    true,
  );
export async function all<T>(path: string): Promise<T[]> {
  const items: T[] = [];
  let cursor: string | null = null;
  do {
    const page: PageEnvelope<T> = await read(
      `${path}${path.includes("?") ? "&" : "?"}limit=100${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ""}`,
    );
    items.push(...page.items);
    cursor = page.nextCursor;
  } while (cursor);
  return items;
}
export const session = (id: string) => read<Session>(`learning/sessions/${id}`);
export const mutateSession = (
  id: string,
  action: string,
  body: unknown,
  version: string,
  method = "POST",
) => send<Mutation>(`learning/sessions/${id}/${action}`, body, version, method);
