# Chrome DevTools MCP helpers for Blazor Server pages

Use with `evaluate_script`. Prefer them over `take_snapshot` loops: snapshots are large and uids change after every render.

## Set a Blazor-bound input or textarea

`fill` works with a fresh uid; when the uid is stale, use the native setter plus an `input` event so Blazor sees the change.

```js
(el, v) => {
  const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, v);
  el.dispatchEvent(new Event('input', { bubbles: true }));
}
```

## Click a button by its visible text

```js
() => [...document.querySelectorAll('main button')].find(b => b.innerText.trim() === 'Generar').click()
```

Use `textContent`, not `innerText`, when the query can hit SVG nodes (`innerText` is undefined there).

## Assistant turn helper (install once per page load)

Sends one answer and waits for the next `Asistente:` message or an alert; returns only the new question, the counter and the quick replies.

```js
async () => {
  window.ans = async (text) => {
    const log = () => document.querySelector('[role=log]').innerText;
    const n0 = (log().match(/Asistente:/g) || []).length;
    const t = document.querySelector('textarea');
    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set.call(t, text);
    t.dispatchEvent(new Event('input', { bubbles: true }));
    await new Promise(r => setTimeout(r, 400));
    [...document.querySelectorAll('main form button')].find(b => b.innerText.trim() === 'Enviar').click();
    const t0 = Date.now();
    while (Date.now() - t0 < 75000) {
      await new Promise(r => setTimeout(r, 1000));
      if ((log().match(/Asistente:/g) || []).length > n0) break;
      const alerts = [...document.querySelectorAll('[role=alert]')].map(e => e.innerText).filter(Boolean);
      if (alerts.length) return { error: alerts };
    }
    return {
      count: document.querySelector('main').innerText.match(/(\d+) de \d+ preguntas respondidas/)?.[1],
      q: log().split('Asistente:').pop().trim().slice(0, 520),
    };
  };
  return 'ok';
}
```

## Long operations

Document generation can exceed the 120 s MCP limit; the call then moves to the background. Poll the URL (`/iniciativas/{id}/artefactos/{slug}`) or the alert instead of waiting in one script.

## POSTs that must reach the rate limiter

Fetch the form first, take `__RequestVerificationToken`, and post it. Tokenless POSTs are answered 400 and, as measured on 2026-10-16, never turn into 429 (11 in a row gave 11 × 400), so only tokened POSTs show the 429 page. Send the antiforgery cookie too (a cookie jar).
