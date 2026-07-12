import { useEffect } from 'react';
import { translateTooltip } from '../i18n';

export interface ActionMenuItem {
  Label: string;
  TooltipKey: string;
  Disabled?: boolean;
  Danger?: boolean;
  OnClick: () => void;
}

export interface ActionMenuState {
  Top: number;
  Left: number;
}

interface ActionMenuProps {
  State: ActionMenuState | null;
  Items: ActionMenuItem[];
  OnClose: () => void;
}

export function openActionMenuFromButton(button: HTMLButtonElement, width = 190): ActionMenuState {
  const rect = button.getBoundingClientRect();
  return {
    Top: rect.bottom + 6,
    Left: Math.max(8, Math.min(window.innerWidth - width - 8, rect.right - width))
  };
}

export default function ActionMenu({ State, Items, OnClose }: ActionMenuProps) {
  useEffect(() => {
    if (!State) return;

    window.addEventListener('click', OnClose);
    window.addEventListener('scroll', OnClose, true);
    window.addEventListener('resize', OnClose);

    return () => {
      window.removeEventListener('click', OnClose);
      window.removeEventListener('scroll', OnClose, true);
      window.removeEventListener('resize', OnClose);
    };
  }, [State, OnClose]);

  if (!State) return null;

  return (
    <div
      className="floating-action-menu"
      style={{ top: `${State.Top}px`, left: `${State.Left}px` }}
      onClick={event => event.stopPropagation()}
    >
      {Items.map(item => (
        <button
          key={item.Label}
          type="button"
          className={item.Danger ? 'danger-menu-item' : undefined}
          title={translateTooltip(item.TooltipKey)}
          disabled={item.Disabled}
          onClick={() => {
            OnClose();
            item.OnClick();
          }}
        >
          {item.Label}
        </button>
      ))}
    </div>
  );
}

export function EllipsisIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="1" />
      <circle cx="19" cy="12" r="1" />
      <circle cx="5" cy="12" r="1" />
    </svg>
  );
}
