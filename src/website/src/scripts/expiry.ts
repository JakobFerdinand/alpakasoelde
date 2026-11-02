// The site is static, so a Veranstaltung that ends after the last build would
// still be listed. The server markup carries each expiry as an ISO timestamp
// and this runs in the visitor's browser to tidy up (see docs/adr).
//
//   data-expires-at       remove the element once the time has passed
//   data-remove-when-empty  remove the container once no data-expires-at is left
//   data-reveal-at        un-hide the element once the time has passed
export const applyExpiry = (root: ParentNode, now: Date) => {
  const passed = (el: Element, attribute: string) =>
    new Date(el.getAttribute(attribute)!).getTime() <= now.getTime();

  root.querySelectorAll('[data-expires-at]').forEach((el) => {
    if (passed(el, 'data-expires-at')) el.remove();
  });

  root.querySelectorAll('[data-remove-when-empty]').forEach((el) => {
    if (!el.querySelector('[data-expires-at]')) el.remove();
  });

  root.querySelectorAll<HTMLElement>('[data-reveal-at]').forEach((el) => {
    if (passed(el, 'data-reveal-at')) el.hidden = false;
  });
};
