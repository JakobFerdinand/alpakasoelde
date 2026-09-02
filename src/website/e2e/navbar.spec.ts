import { expect, test } from '@playwright/test';

// The burger menu only exists below the 768px breakpoint.
test.use({ viewport: { width: 390, height: 844 } });

test('the burger menu opens, navigates and closes itself again', async ({ page }) => {
  await page.goto('/');

  const toggle = page.getByRole('button', { name: 'Menü öffnen' });
  const links = page.locator('#primary-navigation');

  await expect(toggle).toBeVisible();
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  await expect(links).toBeHidden();

  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-expanded', 'true');
  await expect(links).toBeVisible();

  await links.getByRole('link', { name: 'Kontakt' }).click();

  // Following a link closes the menu instead of leaving it covering the page.
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  await expect(links).toBeHidden();
  await expect(page.locator('#kontakt')).toBeVisible();
});
