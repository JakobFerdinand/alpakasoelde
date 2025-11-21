import { expect, test } from '@playwright/test';

test.describe('Homepage navigation', () => {
  test('renders hero content and CTA', async ({ page }) => {
    await page.goto('/');

    await expect(
      page.getByText('Erlebe unvergessliche Momente mit unseren Alpakas in Frauenstein.')
    ).toBeVisible();
    await expect(page.getByRole('link', { name: 'Jetzt Alpaka-Tour buchen' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Jetzt Alpaka-Tour buchen' })).toHaveAttribute(
      'href',
      '#kontakt'
    );
  });

  test('mobile menu toggles visibility and resets on navigation', async ({ page }) => {
    await page.setViewportSize({ width: 600, height: 900 });
    await page.goto('/');

    const menuToggle = page.getByRole('button', { name: 'Menü öffnen' });
    const navList = page.locator('#primary-navigation');
    const alpakaNavLink = page
      .locator('nav')
      .getByRole('link', { name: 'Alpakas', exact: true })
      .first();

    await expect(menuToggle).toHaveAttribute('aria-expanded', 'false');
    await menuToggle.click();
    await expect(menuToggle).toHaveAttribute('aria-expanded', 'true');
    await expect(navList).toHaveClass(/open/);

    await alpakaNavLink.click();
    await expect(menuToggle).toHaveAttribute('aria-expanded', 'false');
    await expect(navList).not.toHaveClass(/open/);
  });
});
