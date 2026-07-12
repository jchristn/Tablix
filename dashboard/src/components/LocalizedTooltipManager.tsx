import { useEffect } from 'react';
import { getLanguage, translateTooltip, type DashboardLanguage } from '../i18n';

const controlSelector = 'button, a, input, select, textarea, [role="button"], [tabindex="0"]';

export default function LocalizedTooltipManager() {
  useEffect(() => {
    function applyTooltips() {
      const language = getLanguage();
      const controls: Element[] = Array.from(document.querySelectorAll(controlSelector));
      controls.forEach(control => applyTooltip(control, language));
    }

    const observer = new MutationObserver(() => applyTooltips());
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-label', 'placeholder', 'data-tooltip-key', 'disabled'] });
    window.addEventListener('tablix-language-changed', applyTooltips);
    applyTooltips();

    return () => {
      observer.disconnect();
      window.removeEventListener('tablix-language-changed', applyTooltips);
    };
  }, []);

  return null;
}

function applyTooltip(control: Element, language: DashboardLanguage) {
  if (!(control instanceof HTMLElement)) return;

  const key = control.dataset.tooltipKey;
  if (key) {
    control.title = translateTooltip(key, language);
    return;
  }

  control.title = buildFallbackTooltip(control, language);
}

function buildFallbackTooltip(control: HTMLElement, language: DashboardLanguage) {
  const label = extractLabel(control);
  if (control instanceof HTMLAnchorElement)
    return formatTooltip('generic.link', label, language);

  if (control instanceof HTMLTextAreaElement)
    return translateTooltip('generic.textarea', language);

  if (control instanceof HTMLSelectElement)
    return translateTooltip('generic.select', language);

  if (control instanceof HTMLInputElement) {
    if (control.type === 'checkbox')
      return translateTooltip('generic.checkbox', language);
    return translateTooltip('generic.input', language);
  }

  if (control instanceof HTMLButtonElement)
    return formatTooltip('generic.button', label, language);

  return translateTooltip('generic.control', language);
}

function formatTooltip(key: string, label: string, language: DashboardLanguage) {
  return translateTooltip(key, language).replace('{label}', label || 'control');
}

function extractLabel(control: HTMLElement) {
  const ariaLabel = control.getAttribute('aria-label');
  if (ariaLabel && ariaLabel.trim()) return ariaLabel.trim();

  const placeholder = control.getAttribute('placeholder');
  if (placeholder && placeholder.trim()) return placeholder.trim();

  const text = control.textContent;
  if (text && text.trim()) return text.trim().replace(/\s+/g, ' ').slice(0, 80);

  const name = control.getAttribute('name');
  if (name && name.trim()) return name.trim();

  const id = control.getAttribute('id');
  if (id && id.trim()) return id.trim();

  return 'control';
}
