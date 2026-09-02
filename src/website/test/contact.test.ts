import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { expect, test } from 'vitest';
import Contact from '../src/components/Contact.astro';

const render = async () => {
  const container = await AstroContainer.create();
  return container.renderToString(Contact);
};

test('the contact form submits without JavaScript', async () => {
  const html = await render();

  const form = html.match(/<form[^>]*id="contact-form"[^>]*>/)?.[0];
  expect(form, 'no #contact-form was rendered').toBeTruthy();

  // A plain urlencoded POST is what the API parses; an enctype override or a
  // JSON fetch would break submission with scripting turned off.
  expect(form).toContain('method="post"');
  expect(form).toContain('action="/api/send-message"');
  expect(form).not.toContain('enctype');
  expect(html).toContain('type="submit"');
});

test('the form field limits match what the API accepts', async () => {
  const html = await render();

  const field = (name: string) =>
    html.match(new RegExp(`<(?:input|textarea)[^>]*name="${name}"[^>]*>`))?.[0];

  // Mirrors the constants in website-api SendMessage.Handler; a field that lets
  // more through than the API accepts turns into a 400 the visitor cannot fix.
  expect(field('name')).toContain('maxlength="100"');
  expect(field('email')).toContain('maxlength="254"');
  expect(field('phone')).toContain('maxlength="30"');
  expect(field('message')).toContain('maxlength="2000"');
});

test('the fields the API treats as mandatory are required in the markup', async () => {
  const html = await render();

  const field = (name: string) =>
    html.match(new RegExp(`<(?:input|textarea)[^>]*name="${name}"[^>]*>`))?.[0];

  expect(field('name')).toContain('required');
  expect(field('email')).toContain('required');
  expect(field('message')).toContain('required');
  // Phone is optional on both sides.
  expect(field('phone')).not.toContain('required');
});
