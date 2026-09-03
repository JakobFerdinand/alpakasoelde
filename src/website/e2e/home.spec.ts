import { expect, test } from '@playwright/test';

test('the landing page renders its hero and links into the contact section', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle('Willkommen | Alpakasölde');
  await expect(page.getByRole('heading', { level: 1, name: 'Alpakasölde' })).toBeAttached();

  const cta = page.getByRole('link', { name: 'Jetzt Alpaka-Tour buchen' });
  await expect(cta).toBeVisible();
  await cta.click();
  await expect(page.locator('#kontakt')).toBeVisible();
});

test('the footer navigates to the Impressum page', async ({ page }) => {
  await page.goto('/');

  await page.getByRole('link', { name: 'Impressum' }).click();

  await expect(page).toHaveURL('/impressum');
  await expect(page.getByRole('heading', { name: 'Impressum' })).toBeVisible();
});
