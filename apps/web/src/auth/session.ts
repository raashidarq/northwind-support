import { useSyncExternalStore } from 'react';
import type { LoginResponse } from '../types/api';

/**
 * The signed-in agent and their access token, kept in sessionStorage: it survives a page reload
 * but is gone when the tab is closed. Anything that changes it tells the listeners, so the app
 * (header, route guard) re-renders straight away.
 */
const storageKey = 'northwind.session';

const listeners = new Set<() => void>();

let cached: { raw: string | null; session: LoginResponse | null } = { raw: null, session: null };

/** The current session, or null when nobody is signed in or the token has expired. */
export function getSession(): LoginResponse | null {
  const raw = sessionStorage.getItem(storageKey);

  // Same string, same object: useSyncExternalStore needs a stable value between changes.
  if (raw !== cached.raw) {
    cached = { raw, session: raw ? (JSON.parse(raw) as LoginResponse) : null };
  }

  const session = cached.session;

  return session && new Date(session.expiresAtUtc).getTime() > Date.now() ? session : null;
}

export function saveSession(session: LoginResponse) {
  sessionStorage.setItem(storageKey, JSON.stringify(session));
  notify();
}

export function clearSession() {
  if (sessionStorage.getItem(storageKey) === null) {
    return;
  }

  sessionStorage.removeItem(storageKey);
  notify();
}

/** The current session, re-rendering the component whenever it changes. */
export function useSession(): LoginResponse | null {
  return useSyncExternalStore(subscribe, getSession);
}

function subscribe(listener: () => void) {
  listeners.add(listener);

  return () => listeners.delete(listener);
}

function notify() {
  listeners.forEach((listener) => listener());
}
