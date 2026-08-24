type Trigger = 'swipe' | 'button' | 'thumb' | 'key' | 'autoplay';

type EmitName =
  | 'slide_change'
  | 'lightbox_open'
  | 'lightbox_close'
  | 'gallery_autoplay_stop';

type ViewTransitionDocument = Document & {
  startViewTransition?: (updateCallback: () => void) => { finished: Promise<void> };
};

const prefersReducedMotion = (): boolean =>
  matchMedia('(prefers-reduced-motion: reduce)').matches;

function initGallery(root: HTMLElement): void {
  const track = root.querySelector<HTMLUListElement>('[data-gallery-track]');
  if (!track || track.dataset.bound) return;
  track.dataset.bound = 'true';

  const slides = Array.from(track.children) as HTMLElement[];
  const imgs = Array.from(track.querySelectorAll('img'));
  const count = slides.length;
  if (!count) return;

  const variant = root.dataset.variant === 'story' ? 'story' : 'product';
  const galleryId = root.dataset.galleryId ?? '';
  const canWrap = root.dataset.loop === 'wrap-buttons';
  const writable = variant === 'product' && galleryId !== '';

  const prevButton = root.querySelector<HTMLButtonElement>('[data-gallery-prev]');
  const nextButton = root.querySelector<HTMLButtonElement>('[data-gallery-next]');
  const dialog = root.querySelector<HTMLDialogElement>('[data-gallery-lightbox]');
  const zoomImg = root.querySelector<HTMLImageElement>('[data-gallery-zoom-img]');
  const zoomport = root.querySelector<HTMLElement>('[data-gallery-zoomport]');
  const lbCaption = root.querySelector<HTMLElement>('[data-gallery-lb-caption]');
  const lbCurrent = root.querySelector<HTMLElement>('[data-gallery-lb-current]');
  const toggleButton = root.querySelector<HTMLButtonElement>('[data-gallery-toggle]');

  let index = 0;
  let suppress = false;
  let lastFocus: Element | null = null;
  let autoplayTimer: ReturnType<typeof setInterval> | undefined;
  let autoplayDead = false;
  let userPaused = false;
  let inView = false;
  let hovering = false;
  let focusing = false;

  const slideWidth = (): number => slides[0]?.offsetWidth ?? 0;

  const clampIndex = (i: number): number =>
    canWrap ? (i + count) % count : Math.max(0, Math.min(count - 1, i));

  const emit = (name: EmitName, detail: Record<string, unknown> = {}): void => {
    root.dispatchEvent(
      new CustomEvent(name, {
        bubbles: true,
        detail: { path: location.pathname, galleryId, ...detail },
      }),
    );
  };

  const setToggleUi = (playing: boolean): void => {
    if (!toggleButton) return;
    toggleButton.setAttribute('aria-pressed', playing ? 'false' : 'true');
    toggleButton.setAttribute('aria-label', playing ? 'Diashow pausieren' : 'Diashow abspielen');
  };

  const render = (trigger?: Trigger): void => {
    for (const el of root.querySelectorAll<HTMLElement>('[data-gallery-dot], [data-gallery-thumb]')) {
      const active = Number(el.dataset.index) === index;
      if (active) el.setAttribute('aria-current', 'true');
      else el.removeAttribute('aria-current');
      el.tabIndex = active ? 0 : -1;
    }
    for (const el of root.querySelectorAll<HTMLElement>('[data-gallery-caption]')) {
      if (Number(el.dataset.index) === index) el.setAttribute('data-active', '');
      else el.removeAttribute('data-active');
    }
    const counter = root.querySelector('[data-gallery-counter-current]');
    if (counter) counter.textContent = String(index + 1);
    const status = root.querySelector('[data-gallery-status]');
    if (status) status.textContent = `Bild ${index + 1} von ${count}: ${imgs[index]?.alt ?? ''}`;
    if (writable) history.replaceState(null, '', `#${galleryId}-${index + 1}`);
    if (trigger) emit('slide_change', { variant, index: index + 1, trigger });
  };

  const stopAutoplayTimer = (): void => {
    if (autoplayTimer !== undefined) {
      clearInterval(autoplayTimer);
      autoplayTimer = undefined;
    }
  };

  const stopAutoplayPermanently = (): void => {
    stopAutoplayTimer();
    if (autoplayDead) return;
    autoplayDead = true;
    emit('gallery_autoplay_stop');
    setToggleUi(false);
  };

  const scrollToSlide = (target: number, behavior: ScrollBehavior): void => {
    const left = target * slideWidth();
    if (Math.round(track.scrollLeft) === Math.round(left)) return;
    suppress = true;
    track.scrollTo({ left, behavior: prefersReducedMotion() ? 'auto' : behavior });
  };

  const goTo = (i: number, trigger?: Trigger, behavior: ScrollBehavior = 'smooth'): void => {
    if (trigger && trigger !== 'autoplay') stopAutoplayPermanently();
    index = clampIndex(i);
    scrollToSlide(index, behavior);
    render(trigger);
  };

  const syncFromScroll = (): void => {
    if (suppress) {
      suppress = false;
      return;
    }
    const width = slideWidth();
    if (!width) return;
    const next = clampIndex(Math.round(track.scrollLeft / width));
    if (next !== index) {
      index = next;
      render('swipe');
    }
  };

  const supportsScrollend = 'onscrollend' in window;
  if (supportsScrollend) {
    track.addEventListener('scrollend', syncFromScroll);
  } else {
    let debounce: ReturnType<typeof setTimeout> | undefined;
    track.addEventListener(
      'scroll',
      () => {
        if (debounce !== undefined) clearTimeout(debounce);
        debounce = setTimeout(syncFromScroll, 120);
      },
      { passive: true },
    );
  }

  prevButton?.addEventListener('click', () => goTo(index - 1, 'button'));
  nextButton?.addEventListener('click', () => goTo(index + 1, 'button'));

  for (const el of root.querySelectorAll<HTMLElement>('[data-gallery-dot], [data-gallery-thumb]')) {
    el.addEventListener('click', () => goTo(Number(el.dataset.index), 'thumb'));
  }

  root.addEventListener('keydown', (event) => {
    if (event.defaultPrevented) return;
    if (event.target instanceof Element && event.target.closest('dialog')) return;
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      goTo(index - 1, 'key');
      return;
    }
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      goTo(index + 1, 'key');
      return;
    }
    if (event.key === 'Home') {
      event.preventDefault();
      goTo(0, 'key');
      return;
    }
    if (event.key === 'End') {
      event.preventDefault();
      goTo(count - 1, 'key');
      return;
    }
    if (
      variant === 'product' &&
      (event.key === 'Enter' || event.key === ' ') &&
      event.target instanceof Element &&
      event.target.closest('.gallery__stage') &&
      !event.target.closest('button')
    ) {
      event.preventDefault();
      openLightbox();
    }
  });

  const currentTitle = (): string => slides[index]?.dataset.caption ?? imgs[index]?.alt ?? '';

  const hiResSrc = (i: number): string =>
    slides[i]?.dataset.hiresSrc || imgs[i]?.currentSrc || imgs[i]?.src || '';

  const syncLightboxChrome = (): void => {
    if (!dialog) return;
    dialog.setAttribute('aria-label', `Bildansicht: ${currentTitle()}`);
    if (lbCaption) lbCaption.textContent = currentTitle();
    if (lbCurrent) lbCurrent.textContent = String(index + 1);
  };

  const resetZoomport = (): void => {
    if (!zoomport) return;
    zoomport.classList.remove('is-zoomed');
    zoomport.scrollLeft = 0;
    zoomport.scrollTop = 0;
  };

  function openLightbox(): void {
    if (!dialog || !zoomImg) return;
    lastFocus = document.activeElement;
    const src = hiResSrc(index);
    zoomImg.alt = imgs[index]?.alt ?? '';
    zoomImg.dataset.src = src;
    if (!zoomImg.getAttribute('src')) zoomImg.setAttribute('src', src);
    resetZoomport();
    syncLightboxChrome();
    const open = (): void => {
      dialog.showModal();
      emit('lightbox_open', { index: index + 1 });
    };
    const doc = document as ViewTransitionDocument;
    if (doc.startViewTransition && !prefersReducedMotion()) {
      const name = `gallery-hero-${galleryId || String(index)}`;
      const slideImg = imgs[index];
      slideImg?.style.setProperty('view-transition-name', name);
      zoomImg.style.setProperty('view-transition-name', name);
      doc.startViewTransition(open).finished.finally(() => {
        slideImg?.style.removeProperty('view-transition-name');
        zoomImg.style.removeProperty('view-transition-name');
      });
    } else {
      open();
    }
  }

  const pointZoomImgAt = (i: number): void => {
    if (!zoomImg) return;
    const src = hiResSrc(i);
    zoomImg.alt = imgs[i]?.alt ?? '';
    zoomImg.dataset.src = src;
    zoomImg.setAttribute('src', src);
  };

  const stepLightbox = (dir: number): void => {
    goTo(index + dir, 'button');
    pointZoomImgAt(index);
    resetZoomport();
    syncLightboxChrome();
  };

  root.querySelector('[data-gallery-zoom-open]')?.addEventListener('click', () => openLightbox());
  root.querySelector('[data-gallery-lb-close]')?.addEventListener('click', () => dialog?.close());
  root.querySelector('[data-gallery-lb-prev]')?.addEventListener('click', () => stepLightbox(-1));
  root.querySelector('[data-gallery-lb-next]')?.addEventListener('click', () => stepLightbox(1));

  dialog?.addEventListener('close', () => {
    zoomImg?.style.removeProperty('view-transition-name');
    imgs[index]?.style.removeProperty('view-transition-name');
    resetZoomport();
    emit('lightbox_close', { index: index + 1 });
    if (lastFocus instanceof HTMLElement) lastFocus.focus();
  });

  dialog?.addEventListener('keydown', (event) => {
    if (event.defaultPrevented) return;
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      stepLightbox(-1);
    }
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      stepLightbox(1);
    }
  });

  const toggleZoom = (): void => {
    zoomport?.classList.toggle('is-zoomed');
  };

  zoomport?.addEventListener('dblclick', toggleZoom);
  zoomport?.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    if (event.target instanceof HTMLButtonElement) return;
    event.preventDefault();
    toggleZoom();
  });

  if (matchMedia('(hover: hover) and (pointer: fine)').matches) {
    const preloadNeighbor = (dir: number): void => {
      const img = imgs[clampIndex(index + dir)];
      const src = img?.currentSrc || img?.src;
      if (!src) return;
      const preloaded = new Image();
      preloaded.src = src;
    };
    prevButton?.addEventListener('pointerenter', () => preloadNeighbor(-1));
    nextButton?.addEventListener('pointerenter', () => preloadNeighbor(1));
  }

  if (root.dataset.autoplay === 'true' && !prefersReducedMotion()) {
    const startAutoplay = (): void => {
      if (
        autoplayTimer !== undefined ||
        autoplayDead ||
        userPaused ||
        !inView ||
        hovering ||
        focusing ||
        document.hidden
      ) {
        return;
      }
      autoplayTimer = setInterval(
        () => goTo(index + 1, 'autoplay'),
        Number(root.dataset.intervalMs) || 6000,
      );
    };
    const observer = new IntersectionObserver(
      (entries) => {
        inView = entries.some((entry) => entry.isIntersecting);
        if (inView) startAutoplay();
        else stopAutoplayTimer();
      },
      { threshold: 0.5 },
    );
    observer.observe(root);
    root.addEventListener('mouseenter', () => {
      hovering = true;
      stopAutoplayTimer();
    });
    root.addEventListener('mouseleave', () => {
      hovering = false;
      startAutoplay();
    });
    root.addEventListener('focusin', () => {
      focusing = true;
      stopAutoplayTimer();
    });
    root.addEventListener('focusout', () => {
      focusing = false;
      startAutoplay();
    });
    root.addEventListener('pointerdown', () => stopAutoplayPermanently());
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) stopAutoplayTimer();
      else startAutoplay();
    });
    toggleButton?.addEventListener('click', () => {
      if (autoplayTimer !== undefined) {
        userPaused = true;
        stopAutoplayTimer();
        setToggleUi(false);
      } else {
        userPaused = false;
        autoplayDead = false;
        setToggleUi(true);
        startAutoplay();
      }
    });
    setToggleUi(true);
  }

  if (writable) {
    const matchHash = (): RegExpMatchArray | null =>
      location.hash.match(new RegExp(`^#${galleryId}-(\\d+)$`));
    const matched = matchHash();
    if (matched) {
      const n = Number(matched[1]);
      if (n >= 1 && n <= count) {
        index = n - 1;
        const left = index * slideWidth();
        if (Math.round(track.scrollLeft) !== Math.round(left)) {
          suppress = true;
          track.scrollTo({ left, behavior: 'instant' });
        }
        render();
      }
    }
    addEventListener('hashchange', () => {
      const next = matchHash();
      if (!next) return;
      const n = Number(next[1]);
      if (n >= 1 && n <= count && n - 1 !== index) goTo(n - 1);
    });
  }
}

for (const rootEl of document.querySelectorAll<HTMLElement>('[data-gallery]')) {
  initGallery(rootEl);
}
