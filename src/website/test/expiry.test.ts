// @vitest-environment happy-dom
import { expect, test } from 'vitest';
import { applyExpiry } from '../src/scripts/expiry';

// The same markup the homepage section, nav link and detail page render.
const page = `
  <nav><ul><li data-expires-at="2026-06-02T22:00:00.000Z" id="nav"><a href="/#veranstaltungen">Veranstaltungen</a></li></ul></nav>
  <section data-remove-when-empty id="section">
    <h2>Veranstaltungen</h2>
    <a data-expires-at="2026-05-30T10:00:00.000Z" id="yoga">Yoga</a>
    <a data-expires-at="2026-06-02T22:00:00.000Z" id="filzen">Filzen</a>
  </section>
  <p data-reveal-at="2026-05-30T10:00:00.000Z" id="notice" hidden>Diese Veranstaltung hat bereits stattgefunden</p>
`;

const setup = () => {
  document.body.innerHTML = page;
  return (iso: string) => applyExpiry(document, new Date(iso));
};

test('an event that has not ended yet stays listed', () => {
  const run = setup();
  run('2026-05-30T09:59:59.000Z');

  expect(document.getElementById('yoga')).not.toBeNull();
  expect(document.getElementById('section')).not.toBeNull();
  expect(document.getElementById('notice')!.hidden).toBe(true);
});

test('an ended event leaves the list but the section stays while another remains', () => {
  const run = setup();
  run('2026-05-30T10:00:00.000Z');

  expect(document.getElementById('yoga')).toBeNull();
  expect(document.getElementById('filzen')).not.toBeNull();
  expect(document.getElementById('section')).not.toBeNull();
  expect(document.getElementById('nav')).not.toBeNull();
});

test('when the last event ends the section and the nav link vanish together', () => {
  const run = setup();
  run('2026-06-02T22:00:00.000Z');

  expect(document.getElementById('section')).toBeNull();
  expect(document.getElementById('nav')).toBeNull();
});

test('the past notice appears on the detail page once the event has ended', () => {
  const run = setup();
  run('2026-05-30T10:00:00.000Z');

  expect(document.getElementById('notice')!.hidden).toBe(false);
});
