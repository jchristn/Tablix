import ClipboardButton from './ClipboardButton';

interface ResultActionsProps {
  jsonText: string;
  csvText: string;
  filenameBase: string;
}

export default function ResultActions({ jsonText, csvText, filenameBase }: ResultActionsProps) {
  function downloadCsv() {
    const blob = new Blob([csvText], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${filenameBase}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="result-actions">
      <ClipboardButton text={jsonText} title="Copy result JSON" label="Copy result JSON" />
      <button
        type="button"
        className="btn-secondary result-download"
        onClick={downloadCsv}
        title="Download result rows as CSV"
      >
        <DownloadIcon />
        <span>Download CSV</span>
      </button>
    </div>
  );
}

function DownloadIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <path d="M7 10l5 5 5-5" />
      <path d="M12 15V3" />
    </svg>
  );
}
