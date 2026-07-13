import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import ActionMenu, { EllipsisIcon, openActionMenuFromButton, type ActionMenuState } from '../components/ActionMenu';
import { translateTooltip } from '../i18n';
import type { BuildContextResponse, ChatOptionsResponse, DatabaseSummary, EnumerationResult, ModelProviderSummary } from '../types';

export default function DatabaseListPage() {
  const [result, setResult] = useState<EnumerationResult<DatabaseSummary> | null>(null);
  const [providers, setProviders] = useState<ModelProviderSummary[]>([]);
  const [defaultProviderId, setDefaultProviderId] = useState('');
  const [error, setError] = useState('');
  const [skip, setSkip] = useState(0);
  const [jumpPage, setJumpPage] = useState('');
  const [contextTarget, setContextTarget] = useState<DatabaseSummary | null>(null);
  const [contextPrompt, setContextPrompt] = useState('');
  const [contextProviderId, setContextProviderId] = useState('');
  const [contextBusy, setContextBusy] = useState(false);
  const [contextError, setContextError] = useState('');
  const [contextResult, setContextResult] = useState('');
  const [actionMenu, setActionMenu] = useState<DatabaseActionMenuState | null>(null);
  const maxResults = 20;
  const navigate = useNavigate();

  useEffect(() => { loadDatabases(); }, [skip]);
  useEffect(() => { loadChatOptions(); }, []);
  async function loadDatabases() {
    try {
      const response = await apiFetch(`/v1/database?maxResults=${maxResults}&skip=${skip}`);
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to load databases.'); return; }
      const data: EnumerationResult<DatabaseSummary> = await response.json();
      setResult(data);
      setError('');
    } catch {
      setError('Could not connect to server.');
    }
  }

  async function loadChatOptions() {
    try {
      const response = await apiFetch('/v1/chat/options');
      if (!response.ok) return;

      const data: ChatOptionsResponse = await response.json();
      setProviders(data.Providers || []);
      setDefaultProviderId(selectAvailableProviderId(data));
    } catch {
      // Chat options are optional for list rendering.
    }
  }

  const totalRecords = result ? Number(result.TotalRecords) : 0;
  const totalPages = Math.max(1, Math.ceil(totalRecords / maxResults));
  const currentPage = Math.floor(skip / maxResults) + 1;

  function goToPage(page: number) {
    const clamped = Math.max(1, Math.min(page, totalPages));
    setSkip((clamped - 1) * maxResults);
  }

  function handleJump(e: React.FormEvent) {
    e.preventDefault();
    const page = parseInt(jumpPage, 10);
    if (!isNaN(page)) {
      goToPage(page);
      setJumpPage('');
    }
  }

  function openBuildContext(db: DatabaseSummary, e?: React.MouseEvent) {
    if (e) {
      e.preventDefault();
      e.stopPropagation();
    }
    setActionMenu(null);
    if (providers.length === 0 || !db.IsCrawled) return;

    setContextTarget(db);
    setContextPrompt(defaultContextPrompt(db));
    setContextProviderId(defaultProviderId || providers[0]?.Id || '');
    setContextBusy(false);
    setContextError('');
    setContextResult('');
  }

  function openActionMenu(db: DatabaseSummary, e: React.MouseEvent<HTMLButtonElement>) {
    e.preventDefault();
    e.stopPropagation();
    setActionMenu({ ...openActionMenuFromButton(e.currentTarget), Database: db });
  }

  async function deleteDatabase(db: DatabaseSummary) {
    setActionMenu(null);
    if (!confirm(`Delete database '${db.Id}'?`)) return;

    try {
      const response = await apiFetch(`/v1/database/${db.Id}`, { method: 'DELETE' });
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to delete database.'); return; }
      await loadDatabases();
    } catch {
      setError('Could not connect to server.');
    }
  }

  function closeBuildContext() {
    if (contextBusy) return;
    setContextTarget(null);
    setContextError('');
    setContextResult('');
  }

  async function submitBuildContext(e: React.FormEvent) {
    e.preventDefault();
    if (!contextTarget) return;

    setContextBusy(true);
    setContextError('');
    setContextResult('');

    try {
      const response = await apiFetch(`/v1/database/${contextTarget.Id}/context/build`, {
        method: 'POST',
        body: JSON.stringify({
          ProviderId: contextProviderId || null,
          Prompt: contextPrompt,
        }),
      });

      if (response.status === 401) { navigate('/login'); return; }

      const data = await response.json();
      if (!response.ok || !data.Success) {
        const parts = [data.Error, data.Description, data.Message].filter(Boolean);
        setContextError(parts.length > 0 ? parts.join(': ') : 'Context build failed.');
        return;
      }

      const buildResponse: BuildContextResponse = data;
      setContextResult(buildResponse.Context || '');
      await loadDatabases();
    } catch {
      setContextError('Could not connect to server.');
    } finally {
      setContextBusy(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h2 title="Configured database connections">Databases</h2>
        <button className="btn-primary" title="Add a new database connection" onClick={() => navigate('/databases/new')}>Add Database</button>
      </div>

      {error && <p className="error-text">{error}</p>}

      {result && (
        <div className="card">
          <div className="table-list-header">
            <span className="muted-text" title="Total number of configured databases">{totalRecords} record{totalRecords !== 1 ? 's' : ''}</span>

            <div className="table-list-controls">
              <button
                className="btn-secondary"
                disabled={currentPage <= 1}
                onClick={() => goToPage(currentPage - 1)}
                title="Go to the previous page"
              >
                Prev
              </button>

              <span className="muted-text" title="Current page position">
                Page {currentPage} of {totalPages}
              </span>

              <button
                className="btn-secondary"
                disabled={result.EndOfResults}
                onClick={() => goToPage(currentPage + 1)}
                title="Go to the next page"
              >
                Next
              </button>

              <form onSubmit={handleJump} className="page-jump">
                <input
                  type="number"
                  min={1}
                  max={totalPages}
                  value={jumpPage}
                  onChange={e => setJumpPage(e.target.value)}
                  placeholder="#"
                  title="Enter a page number to jump to"
                />
                <button type="submit" className="btn-secondary" title="Jump to the specified page">Go</button>
              </form>

              <button
                className="btn-secondary"
                onClick={loadDatabases}
                title="Reload the database list from the server"
              >
                Refresh
              </button>
            </div>
          </div>

          <table className="data-table wide-table">
            <thead>
              <tr>
                <th title="Unique database entry identifier">ID</th>
                <th title="Human-readable display name">Name</th>
                <th title="Database engine type">Type</th>
                <th title="Database schema">Schema</th>
                <th className="actions-column" title={translateTooltip('actions.open')}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {result.Objects.map(db => (
                <tr key={db.Id} title="Click to view database details and schema geometry" style={{ cursor: 'pointer' }} onClick={() => navigate(`/databases/${db.Id}`)}>
                  <td><Link to={`/databases/${db.Id}`} title={'View details for ' + db.Id}>{db.Id}</Link></td>
                  <td title={db.Name || '-'}>{db.Name || '-'}</td>
                  <td title={db.Type}>{db.Type}</td>
                  <td title={db.Schema || '-'}>{db.Schema || '-'}</td>
                  <td>
                    <button
                      type="button"
                      className="icon-action row-actions-button"
                      title={translateTooltip('actions.open')}
                      aria-label={`Open actions for ${db.Id}`}
                      onClick={event => openActionMenu(db, event)}
                    >
                      <EllipsisIcon />
                    </button>
                  </td>
                </tr>
              ))}
              {result.Objects.length === 0 && (
                <tr><td colSpan={5} style={{ textAlign: 'center' }} className="muted-text">No databases configured.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      <ActionMenu
        State={actionMenu}
        OnClose={() => setActionMenu(null)}
        Items={actionMenu ? [
          {
            Label: 'Build Context',
            TooltipKey: 'actions.buildContext',
            Disabled: providers.length === 0 || !actionMenu.Database.IsCrawled,
            OnClick: () => openBuildContext(actionMenu.Database)
          },
          {
            Label: 'Delete',
            TooltipKey: 'actions.delete',
            Danger: true,
            OnClick: () => deleteDatabase(actionMenu.Database)
          }
        ] : []}
      />

      {contextTarget && (
        <div className="modal-backdrop" role="presentation" onClick={closeBuildContext}>
          <div className="modal-panel context-build-modal" role="dialog" aria-modal="true" aria-labelledby="build-context-title" onClick={event => event.stopPropagation()}>
            <div className="modal-header">
              <div>
                <h3 id="build-context-title">Build Context</h3>
                <p className="muted-text">{contextTarget.Id}</p>
              </div>
              <button type="button" className="icon-action" aria-label="Close build context dialog" title="Close" onClick={closeBuildContext} disabled={contextBusy}>
                <CloseIcon />
              </button>
            </div>

            <form onSubmit={submitBuildContext} className="context-build-form">
              <div className="form-group">
                <label title="Select which configured model provider should generate context">Provider</label>
                <select value={contextProviderId} onChange={event => setContextProviderId(event.target.value)} disabled={contextBusy || providers.length === 0}>
                  {providers.map(provider => (
                    <option key={provider.Id} value={provider.Id}>{provider.Name || provider.Id} - {provider.Model || provider.Type}</option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label title="Instructions sent to the model along with the last crawled schema">Prompt</label>
                <textarea
                  value={contextPrompt}
                  onChange={event => setContextPrompt(event.target.value)}
                  rows={10}
                  disabled={contextBusy}
                />
              </div>

              {contextError && <p className="error-text">{contextError}</p>}
              {contextResult && (
                <div className="context-result">
                  <strong>Saved context</strong>
                  <pre>{contextResult}</pre>
                </div>
              )}

              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={closeBuildContext} disabled={contextBusy}>Close</button>
                <button type="submit" className="btn-primary" disabled={contextBusy || !contextPrompt.trim() || !contextProviderId}>
                  {contextBusy ? 'Building...' : 'Build and Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

interface DatabaseActionMenuState extends ActionMenuState {
  Database: DatabaseSummary;
}

function defaultContextPrompt(db: DatabaseSummary) {
  const name = db.Name || db.DatabaseName || db.Filename || db.Id;
  return `Analyze the last successful crawl for ${name} and produce concise, durable context for future database chat and MCP usage.

Include:
- The likely purpose of the database when inferable from table and column names.
- Major entities and workflow groupings.
- Declared relationships and clearly labeled inferred relationships.
- Important naming conventions, key columns, and join paths.
- Safe query guidance that respects the configured allowed query types.

Do not include credentials, secrets, raw result rows, or unrelated commentary.`;
}

function selectAvailableProviderId(options: ChatOptionsResponse) {
  if (options.DefaultProviderId && options.Providers.some(provider => provider.Id === options.DefaultProviderId)) {
    return options.DefaultProviderId;
  }

  return options.Providers[0]?.Id || '';
}

function CloseIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}
