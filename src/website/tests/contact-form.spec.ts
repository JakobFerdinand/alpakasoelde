import { expect, test } from '@playwright/test';

test.describe('Contact form validation', () => {
  test('enforces max lengths via custom validation messages', async ({ page }) => {
    await page.goto('/#kontakt');

    const nameInput = page.getByLabel('Name*');
    const emailInput = page.getByLabel('E-Mail*');

    await nameInput.evaluate((input: HTMLInputElement) => {
      input.removeAttribute('maxLength');
      input.value = 'a'.repeat(101);
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
    await emailInput.fill('user@example.com');

    await expect(nameInput).toHaveJSProperty('validationMessage', 'Maximal 100 Zeichen erlaubt');
    await expect(emailInput).toHaveJSProperty('validationMessage', '');
  });

  test('disables submit while sending when inputs are valid', async ({ page }) => {
    await page.route('**/api/send-message', (route) => route.fulfill({ status: 200, body: 'ok' }));
    await page.goto('/#kontakt');

    await page.evaluate(() => {
      document.getElementById('contact-form')?.addEventListener(
        'submit',
        (event) => event.preventDefault(),
        { once: true }
      );
    });

    await page.getByLabel('Name*').fill('Maria Muster');
    await page.getByLabel('E-Mail*').fill('maria@example.com');
    await page.getByLabel('Nachricht*').fill('Hallo, ich interessiere mich für eine Alpaka-Wanderung.');

    const submitButton = page.getByRole('button', { name: 'Senden' });
    await expect(submitButton).toBeEnabled();

    await submitButton.click();
    await expect(submitButton).toBeDisabled();
  });
});
