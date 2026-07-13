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
      <div className="modal-panel" role="dialog" aria-modal="true" aria-labelledby="record-view-title" onClick={event => event.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 id="record-view-title">{Title}</h3>
            {Subtitle && <p className="muted-text">{Subtitle}</p>}
          </div>
          <button type="button" className="icon-action" aria-label="Close record view" title="Close" onClick={OnClose}>
            <CloseIcon />
          </button>
        </div>

        <table>
          <tbody>
            {Rows.map(row => (
              <tr key={row.Label}>
                <td style={{ fontWeight: 600, width: '190px' }}>{row.Label}</td>
                <td>{formatRecordValue(row.Value)}</td>
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

function formatRecordValue(value: unknown) {
  if (value === null || value === undefined || value === '') return <span className="muted-text">-</span>;
  if (Array.isArray(value)) return value.length > 0 ? value.join(', ') : <span className="muted-text">-</span>;
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'object') {
    return <pre style={{ margin: 0, whiteSpace: 'pre-wrap', fontFamily: 'var(--font-mono)', fontSize: '12px' }}>{JSON.stringify(value, null, 2)}</pre>;
  }

  return String(value);
}

function CloseIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}
