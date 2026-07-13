interface ConfirmDialogProps {
  Open: boolean;
  Title: string;
  Message: string;
  ConfirmLabel?: string;
  CancelLabel?: string;
  Busy?: boolean;
  Danger?: boolean;
  OnConfirm: () => void;
  OnCancel: () => void;
}

export default function ConfirmDialog({
  Open,
  Title,
  Message,
  ConfirmLabel = 'Confirm',
  CancelLabel = 'Cancel',
  Busy = false,
  Danger = false,
  OnConfirm,
  OnCancel,
}: ConfirmDialogProps) {
  if (!Open) return null;

  return (
    <div className="modal-backdrop confirm-backdrop" role="presentation" onClick={() => !Busy && OnCancel()}>
      <div className="modal-panel confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-dialog-title" aria-describedby="confirm-dialog-message" onClick={event => event.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 id="confirm-dialog-title">{Title}</h3>
            <p id="confirm-dialog-message" className="muted-text">{Message}</p>
          </div>
        </div>
        <div className="modal-actions">
          <button type="button" className="btn-secondary" onClick={OnCancel} disabled={Busy}>{CancelLabel}</button>
          <button type="button" className={Danger ? 'btn-danger' : 'btn-primary'} onClick={OnConfirm} disabled={Busy}>
            {Busy ? 'Working...' : ConfirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
