import { useEffect } from 'react';
import {
  formatLocalizedTooltip,
  getLanguage,
  getLanguageDirection,
  translateAttributeValue,
  translateTooltip,
  translateVisibleText,
  type DashboardLanguage
} from '../i18n';

const controlSelector = 'button, a, input, select, textarea, [role="button"], [tabindex="0"]';
const attributeSelector = '[title], [placeholder], [aria-label]';

export default function LocalizedTooltipManager() {
  useEffect(() => {
    function applyLocalization() {
      const language = getLanguage();
      document.documentElement.lang = language;
      document.documentElement.dir = getLanguageDirection(language);

      applyVisibleText(document.body, language);
      applyAttributes(language);

      const controls: Element[] = Array.from(document.querySelectorAll(controlSelector));
      controls.forEach(control => applyTooltip(control, language));
    }

    const observer = new MutationObserver(() => applyLocalization());
    observer.observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['aria-label', 'placeholder', 'title', 'data-tooltip-key', 'disabled'] });
    window.addEventListener('tablix-language-changed', applyLocalization);
    applyLocalization();

    return () => {
      observer.disconnect();
      window.removeEventListener('tablix-language-changed', applyLocalization);
    };
  }, []);

  return null;
}

function applyVisibleText(root: HTMLElement, language: DashboardLanguage) {
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      if (!(node instanceof Text)) return NodeFilter.FILTER_REJECT;
      if (!node.textContent || !node.textContent.trim()) return NodeFilter.FILTER_REJECT;
      if (shouldSkipTextNode(node)) return NodeFilter.FILTER_REJECT;
      return NodeFilter.FILTER_ACCEPT;
    }
  });

  const nodes: Text[] = [];
  let current = walker.nextNode();
  while (current) {
    if (current instanceof Text) nodes.push(current);
    current = walker.nextNode();
  }

  nodes.forEach(node => {
    const current = node.textContent || '';
    const translated = translateVisibleText(current, language);
    if (translated !== node.textContent) {
      node.textContent = translated;
    }
  });
}

function applyAttributes(language: DashboardLanguage) {
  const elements: Element[] = Array.from(document.querySelectorAll(attributeSelector));
  elements.forEach(element => {
    if (!(element instanceof HTMLElement)) return;
    if (shouldSkipAttributeElement(element)) return;
    translateAttribute(element, 'title', language);
    translateAttribute(element, 'placeholder', language);
    translateAttribute(element, 'aria-label', language);
  });
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
    return formatLocalizedTooltip('generic.link', label, language);

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
    return formatLocalizedTooltip('generic.button', label, language);

  return translateTooltip('generic.control', language);
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

function translateAttribute(element: HTMLElement, attribute: string, language: DashboardLanguage) {
  const current = element.getAttribute(attribute);
  if (!current) return;

  const translated = translateAttributeValue(current, language);
  if (translated !== current) {
    element.setAttribute(attribute, translated);
  }
}

function shouldSkipTextNode(node: Text) {
  const parent = node.parentElement;
  if (!parent) return true;
  return shouldSkipElement(parent);
}

function shouldSkipElement(element: HTMLElement) {
  return Boolean(element.closest('script, style, svg, pre, code, textarea, input, .markdown-body, .database-context-display, .query-results, .result-table, .monaco-editor, [data-i18n-skip="true"]'));
}

function shouldSkipAttributeElement(element: HTMLElement) {
  return Boolean(element.closest('script, style, svg, pre, code, .markdown-body, .database-context-display, .query-results, .result-table, .monaco-editor, [data-i18n-skip="true"]'));
}
