import { expect, test, type Page } from '@playwright/test';

const slideshow = (page: Page) => page.getByRole('region', { name: 'Fotos aus unserem Hofladen' });
const dot = (page: Page, n: number) =>
  slideshow(page).getByRole('button', { name: `Zu Bild ${n} springen` });
const imageButton = (page: Page, n: number) =>
  slideshow(page)
    .getByRole('button', { name: /Bild groß anzeigen/ })
    .nth(n);

// The caption overlays the lower part of each slide and does not forward
// clicks, so aim for the clear image area near the top of the slide.
const imageButtonOptions = { position: { x: 100, y: 60 } } as const;

test.describe('the slideshow deck', () => {
  test('moves the current slide via buttons and direct navigation', async ({ page }) => {
    await page.goto('/produkte');
    await expect(dot(page, 1)).toHaveAttribute('aria-current', 'true');

    // "Vorheriges Bild" from the first slide wraps around to the last slide.
    await slideshow(page).getByRole('button', { name: 'Vorheriges Bild' }).click();
    await expect(dot(page, 6)).toHaveAttribute('aria-current', 'true');
    await expect(dot(page, 1)).not.toHaveAttribute('aria-current');

    await dot(page, 1).click();
    await expect(dot(page, 1)).toHaveAttribute('aria-current', 'true');

    await slideshow(page).getByRole('button', { name: 'Nächstes Bild' }).click();
    await expect(dot(page, 2)).toHaveAttribute('aria-current', 'true');

    await dot(page, 4).click();
    await expect(dot(page, 4)).toHaveAttribute('aria-current', 'true');
    await expect(dot(page, 2)).not.toHaveAttribute('aria-current');
  });
});

test.describe('the slideshow lightbox', () => {
  test('opens from a slide, navigates with keys and returns focus to the slide', async ({
    page,
  }) => {
    await page.goto('/produkte');

    await imageButton(page, 0).click(imageButtonOptions);
    const lightbox = page.getByRole('dialog', { name: 'Bildansicht' });
    await expect(lightbox).toBeVisible();

    const lightboxImage = lightbox.getByRole('img');
    await expect(lightboxImage).toHaveAttribute(
      'alt',
      'Strickgabel aus Holz mit einer angestrickten Kordel aus bunter Zauberwolle',
    );
    await expect(lightbox.getByText('Strickgabel', { exact: true })).toBeVisible();

    // The arrow keys page through the slides while the lightbox is open.
    await page.keyboard.press('ArrowRight');
    await expect(lightbox.getByText('Zauberwolle', { exact: true })).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(lightbox).toBeHidden();

    // The deck shows the slide the lightbox was on, and focus lands on that
    // slide's own expand control instead of getting lost.
    await expect(imageButton(page, 1)).toBeFocused();
    await expect(dot(page, 2)).toHaveAttribute('aria-current', 'true');
  });

  test('closes on a backdrop click but not when the photo itself is clicked', async ({ page }) => {
    await page.goto('/produkte');

    await imageButton(page, 0).click(imageButtonOptions);
    const lightbox = page.getByRole('dialog', { name: 'Bildansicht' });
    await expect(lightbox).toBeVisible();

    // The photo belongs to the zoom/drag surface, so clicking it must keep
    // the lightbox open – proven by the dialog still being navigable here.
    await lightbox.getByRole('img').click();
    await page.keyboard.press('ArrowRight');
    await expect(lightbox.getByText('Zauberwolle', { exact: true })).toBeVisible();

    // A click on the empty area of the fullscreen dialog is a backdrop tap.
    await lightbox.click({ position: { x: 5, y: 200 } });
    await expect(lightbox).toBeHidden();
  });
});

test('autoplay keeps advancing while the slideshow sits idle on screen', async ({ page }) => {
  // Playwright's fake clock freezes Chromium's smooth-scroll animation, so the
  // deck would never reach its scroll target and the position tracking would
  // undo the advance. Jump this test to the instant-scroll behaviour the
  // component already has under prefers-reduced-motion so the autoplay loop
  // itself (timer → advance → re-arm) is what gets exercised.
  await page.addInitScript(() => {
    const original = Element.prototype.scrollTo as (
      this: Element,
      argOrX?: ScrollToOptions | number,
      y?: number,
    ) => void;
    Element.prototype.scrollTo = function (
      this: Element,
      argOrX?: ScrollToOptions | number,
      y?: number,
    ): void {
      // Only the options-object overload carries a behavior to force to
      // 'auto'; the coordinate overload has none.
      if (typeof argOrX === 'object') {
        original.call(this, { ...argOrX, behavior: 'auto' });
        return;
      }
      original.call(this, argOrX ?? 0, y ?? 0);
    };
  });
  await page.clock.install();
  await page.goto('/produkte');

  await slideshow(page).scrollIntoViewIfNeeded();
  // Autoplay pauses on hover and on focus, so keep both away from the deck.
  await page.mouse.move(0, 0);
  // The IntersectionObserver marks the story on-screen asynchronously, and
  // only then does the component arm the 7 s advance timer – on a cold dev
  // server that lands well after the 300 ms a renderer tick usually takes.
  // Fast-forwarding before it fires would run the fake clock past an empty
  // timer queue, so hold until the story reports itself in the running state.
  await expect(slideshow(page)).toHaveAttribute('data-autoplay', 'running');

  await page.clock.runFor(15_000);

  // Two 7 s intervals have passed by now, so the deck cannot still sit on
  // slide 1 or 2 – this guards against autoplay advancing once and stalling.
  await expect(dot(page, 3)).toHaveAttribute('aria-current', 'true');
});

test.describe('without JavaScript', () => {
  test.use({ javaScriptEnabled: false });

  test('the static deck shows the first image with controls absent or disabled', async ({
    page,
  }) => {
    await page.goto('/produkte');
    const story = slideshow(page);

    await expect(story.getByRole('button', { name: 'Nächstes Bild' })).toBeHidden();
    await expect(story.getByRole('button', { name: 'Vorheriges Bild' })).toBeHidden();
    await expect(
      story.getByRole('group', { name: 'Direktnavigation' }).getByRole('button'),
    ).toBeHidden();

    await expect(story.getByRole('group', { name: 'Bild 1 von 6' }).getByRole('img')).toBeVisible();

    const mediaButtons = story.getByRole('button', { name: /Bild groß anzeigen/ });
    await expect(mediaButtons).toHaveCount(6);
    for (const button of await mediaButtons.all()) {
      await expect(button).toBeDisabled();
    }
  });
});
