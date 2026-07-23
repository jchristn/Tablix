export interface RecordViewRow {
  Label: string;
  Value: unknown;
}

interface RecordViewModalProps {
  Open: boolean;
  Title: string;
  Subtitle?: string | null;
  Rows: RecordViewRow[];
  OnClose: () => void;
  Actions?: React.ReactNode;
}

export default function RecordViewModal({ Open, Title, Subtitle, Rows, OnClose, Actions }: RecordViewModalProps) {
  if (!Open) return null;

  return (
    <div className="modal-backdrop" role="presentation" onClick={OnClose}>
      <div className="modal-panel record-view-modal" role="dialog" aria-modal="true" aria-labelledby="record-view-title" onClick={event => event.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 id="record-view-title">{Title}</h3>
            {Subtitle && <p className="muted-text">{Subtitle}</p>}
          </div>
          <button type="button" className="icon-action" aria-label="Close record view" title="Close" onClick={OnClose}>
            <CloseIcon />
          </button>
        </div>

        <table className="record-view-table">
          <tbody>
            {Rows.map(row => (
              <tr key={row.Label}>
                <td className="record-view-label-cell">{row.Label}</td>
                <td className="record-view-value-cell">{formatRecordValue(row.Label, row.Value)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="modal-actions" style={{ marginTop: '16px' }}>
          {Actions}
          <button type="button" className="btn-secondary" onClick={OnClose}>Close</button>
        </div>
      </div>
    </div>
  );
}

export function isInteractiveRowClick(event: React.MouseEvent) {
  const target = event.target;
  return target instanceof Element && Boolean(target.closest('button, a, input, select, textarea, [role="button"], [data-row-click-ignore="true"]'));
}

function formatRecordValue(label: string, value: unknown) {
  if (value === null || value === undefined || value === '') return <span className="muted-text">-</span>;
  if (Array.isArray(value)) return value.length > 0 ? value.join(', ') : <span className="muted-text">-</span>;
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'object') {
    return <pre className="record-view-scroll-value">{JSON.stringify(value, null, 2)}</pre>;
  }

  if (typeof value === 'string' && shouldUseScrollableText(label, value)) {
    return <pre className="record-view-scroll-value">{value}</pre>;
  }

  return String(value);
}

function shouldUseScrollableText(label: string, value: string) {
  return label.trim().toLowerCase() === 'context' || /\r|\n/.test(value);
}

function CloseIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}
