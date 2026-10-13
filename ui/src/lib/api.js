// Bridge to the C# host (WebView2). In a plain browser (npm run dev) a mock answers instead,
// so the UI can be developed without the app.
import { mock } from './mock.js';

const web = window.chrome?.webview;
export const isMock = !web;

let seq = 0;
const pending = new Map();
const listeners = new Map();

function dispatch(msg) {
  if (msg.id) {
    const p = pending.get(msg.id);
    if (!p) return;
    pending.delete(msg.id);
    if (msg.ok) p.resolve(msg.result);
    else {
      const err = new Error(msg.error);
      err.cancelled = !!msg.cancelled;
      p.reject(err);
    }
  } else if (msg.event) {
    for (const cb of listeners.get(msg.event) ?? []) cb(msg.data);
  }
}

if (web) web.addEventListener('message', (e) => dispatch(e.data));

/** Calls a host method; resolves with its result or rejects with Error(message). */
export function call(method, params) {
  if (!web) return mock.call(method, params, (event, data) => dispatch({ event, data }));
  const id = `r${++seq}`;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    web.postMessage({ id, method, params: params ?? null });
  });
}

/** Like call(), but also hands the host the dropped File objects (it gets their real paths). */
export function callWithFiles(method, params, files) {
  if (!web) return mock.call(method, { ...params, names: [...files].map((f) => f.name) }, (event, data) => dispatch({ event, data }));
  const id = `r${++seq}`;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    web.postMessageWithAdditionalObjects({ id, method, params: params ?? null }, files);
  });
}

/** Subscribes to a host event; returns an unsubscribe function. */
export function on(event, cb) {
  if (!listeners.has(event)) listeners.set(event, new Set());
  listeners.get(event).add(cb);
  return () => listeners.get(event).delete(cb);
}
