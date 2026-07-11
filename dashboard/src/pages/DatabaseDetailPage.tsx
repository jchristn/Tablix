import { useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import ClipboardButton from '../components/ClipboardButton';
import type {
  BuildContextResponse,
  ChatOptionsResponse,
  ContextUpdateRequest,
  ContextUpdateResponse,
  CrawlProgressEvent,
  DatabaseDetail,
  ModelProviderSummary,
} from '../types';

interface ParsedSseFrame {
  event: string;
  data: string;
}

export default function DatabaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<DatabaseDetail | null>(null);
  const [error, setError] = useState('');
  const [crawling, setCrawling] = useState(false);
  const [crawlEvents, setCrawlEvents] = useState<CrawlProgressEvent[]>([]);
  const [contextDraft, setContextDraft] = useState('');
  const [editingContext, setEditingContext] = useState(false);
  const [savingContext, setSavingContext] = useState(false);
  const [contextError, setContextError] = useState('');
  const [contextMessage, setContextMessage] = useState('');
  const [providers, setProviders] = useState<ModelProviderSummary[]>([]);
  const [defaultProviderId, setDefaultProviderId] = useState('');
  const [buildContextOpen, setBuildContextOpen] = useState(false);
  const [buildContextPrompt, setBuildContextPrompt] = useState('');
  const [buildContextProviderId, setBuildContextProviderId] = useState('');
  const [buildContextBusy, setBuildContextBusy] = useState(false);
  const [buildContextError, setBuildContextError] = useState('');

  useEffect(() => { loadDetail(); }, [id]);
  useEffect(() => { loadChatOptions(); }, []);

  async function loadDetail() {
    try {
      const response = await apiFetch(`/v1/database/${id}`);
      if (response.status === 401) { navigate('/login'); return; }
      if (response.status === 404) { setError('Database not found.'); return; }
      const data: DatabaseDetail = await response.json();
      setDetail(data);
      setContextDraft(data.Context || '');
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
      setDefaultProviderId(data.DefaultProviderId || data.Providers?.[0]?.Id || '');
    } catch {
      // Chat options are optional for database detail rendering.
    }
  }

  async function handleCrawl() {
    setCrawling(true);
    setCrawlEvents([]);
    try {
      const response = await apiFetch(`/v1/database/${id}/crawl/stream`, { method: 'POST' });
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        const message = await response.text();
        setError(message || 'Crawl failed.');
        return;
      }

      if (!response.body) {
        setError('Crawl stream was not available.');
        return;
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let doneReading = false;

      while (!doneReading) {
        const result = await reader.read();
        doneReading = result.done;
        if (result.value) {
          buffer += decoder.decode(result.value, { stream: !doneReading });
          const frames = buffer.split('\n\n');
          buffer = frames.pop() || '';
          frames.forEach(frame => handleCrawlFrame(frame));
        }
      }

      if (buffer.trim()) {
        handleCrawlFrame(buffer);
      }
    } finally {
      setCrawling(false);
    }
  }

  function handleCrawlFrame(frame: string) {
    const parsed = parseSseFrame(frame);
    if (!parsed.data) return;

    const event: CrawlProgressEvent = JSON.parse(parsed.data);
    setCrawlEvents(previous => [...previous, event]);

    if (event.Detail) {
      setDetail(event.Detail);
      if (!editingContext) {
        setContextDraft(event.Detail.Context || '');
      }
    }

    setError('');
  }

  function parseSseFrame(frame: string): ParsedSseFrame {
    const lines = frame.replace(/\r/g, '').split('\n');
    const dataLines: string[] = [];
    let eventName = 'message';

    lines.forEach(line => {
      if (line.startsWith('event:')) {
        eventName = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trimStart());
      }
    });

    return {
      event: eventName,
      data: dataLines.join('\n'),
    };
  }

  async function handleDelete() {
    if (!confirm('Delete this database entry?')) return;
    const response = await apiFetch(`/v1/database/${id}`, { method: 'DELETE' });
    if (response.status === 401) { navigate('/login'); return; }
    if (!response.ok) {
      const message = await response.text();
      setError(message || 'Failed to delete database.');
      return;
    }
    navigate('/');
  }

  function handleStartContextEdit() {
    setContextDraft(detail?.Context || '');
    setEditingContext(true);
    setContextError('');
    setContextMessage('');
  }

  function handleCancelContextEdit() {
    setContextDraft(detail?.Context || '');
    setEditingContext(false);
    setContextError('');
    setContextMessage('');
  }

  async function handleSaveContext() {
    setSavingContext(true);
    setContextError('');
    setContextMessage('');

    const payload: ContextUpdateRequest = {
      Context: contextDraft.trim().length > 0 ? contextDraft : null,
      Mode: 'replace',
    };

    try {
      const response = await apiFetch(`/v1/database/${id}/context`, {
        method: 'POST',
        body: JSON.stringify(payload),
      });

      if (response.status === 401) {
        navigate('/login');
        return;
      }

      if (!response.ok) {
        const message = await response.text();
        setContextError(message || 'Failed to save context.');
        return;
      }

      const result: ContextUpdateResponse = await response.json();
      setDetail(previous => previous ? { ...previous, Context: result.Context } : previous);
      setContextDraft(result.Context || '');
      setEditingContext(false);
      setContextMessage('Context saved.');
    } catch {
      setContextError('Could not connect to server.');
    } finally {
      setSavingContext(false);
    }
  }

  function openBuildContext() {
    if (!detail) return;

    setBuildContextPrompt(defaultContextPrompt(detail));
    setBuildContextProviderId(defaultProviderId || providers[0]?.Id || '');
    setBuildContextError('');
    setBuildContextOpen(true);
  }

  function closeBuildContext() {
    if (buildContextBusy) return;
    setBuildContextOpen(false);
    setBuildContextError('');
  }

  async function handleBuildContext(e: React.FormEvent) {
    e.preventDefault();
    if (!detail) return;

    setBuildContextBusy(true);
    setBuildContextError('');

    try {
      const response = await apiFetch(`/v1/database/${detail.DatabaseId}/context/build`, {
        method: 'POST',
        body: JSON.stringify({
          ProviderId: buildContextProviderId || null,
          Prompt: buildContextPrompt,
        }),
      });

      if (response.status === 401) {
        navigate('/login');
        return;
      }

      const data = await response.json();
      if (!response.ok || !data.Success) {
        const parts = [data.Error, data.Description, data.Message].filter(Boolean);
        setBuildContextError(parts.length > 0 ? parts.join(': ') : 'Context build failed.');
        return;
      }

      const result: BuildContextResponse = data;
      setDetail(previous => previous ? { ...previous, Context: result.Context } : previous);
      setContextDraft(result.Context || '');
      setEditingContext(false);
      setContextMessage('Context built and saved.');
      setBuildContextOpen(false);
    } catch {
      setBuildContextError('Could not connect to server.');
    } finally {
      setBuildContextBusy(false);
    }
  }

  if (error) return <p className="error-text">{error}</p>;
  if (!detail) return <p className="muted-text">Loading...</p>;

  return (
    <div>
      <div className="page-header">
        <div className="detail-title-row">
          <button
            type="button"
            className="icon-action"
            title="Back to database list"
            aria-label="Back to database list"
            onClick={() => navigate('/')}
          >
            <BackIcon />
          </button>
          <h2 title="Database entry details and schema geometry">{detail.DatabaseName || detail.DatabaseId}</h2>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button className="btn-primary" title="Re-discover tables, columns, foreign keys, and indexes" onClick={handleCrawl} disabled={crawling}>
            {crawling ? 'Crawling...' : 'Crawl'}
          </button>
          <button
            className="btn-secondary"
            title={detail.IsCrawled ? 'Generate and save database context from the last successful crawl' : 'Crawl this database before building context'}
            onClick={openBuildContext}
            disabled={!detail.IsCrawled || providers.length === 0}
          >
            Build Context
          </button>
          <button className="btn-secondary" title="Edit this database entry's connection settings and context" onClick={() => navigate(`/databases/${id}/edit`)}>Edit</button>
          <button className="btn-danger" title="Permanently remove this database entry from the configuration" onClick={handleDelete}>Delete</button>
        </div>
      </div>

      {buildContextOpen && (
        <div className="modal-backdrop" role="presentation" onClick={closeBuildContext}>
          <div className="modal-panel context-build-modal" role="dialog" aria-modal="true" aria-labelledby="build-context-title" onClick={event => event.stopPropagation()}>
            <div className="modal-header">
              <div>
                <h3 id="build-context-title">Build Context</h3>
                <p className="muted-text">{detail.DatabaseId}</p>
              </div>
              <button type="button" className="icon-action" aria-label="Close build context dialog" title="Close" onClick={closeBuildContext} disabled={buildContextBusy}>
                <CloseIcon />
              </button>
            </div>

            <form onSubmit={handleBuildContext} className="context-build-form">
              <div className="form-group">
                <label title="Select which configured model provider should generate context">Provider</label>
                <select value={buildContextProviderId} onChange={event => setBuildContextProviderId(event.target.value)} disabled={buildContextBusy || providers.length === 0}>
                  {providers.map(provider => (
                    <option key={provider.Id} value={provider.Id}>{provider.Name || provider.Id} - {provider.Model || provider.Type}</option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label title="Instructions sent to the model along with the last crawled schema">Prompt</label>
                <textarea
                  value={buildContextPrompt}
                  onChange={event => setBuildContextPrompt(event.target.value)}
                  rows={10}
                  disabled={buildContextBusy}
                />
              </div>

              {buildContextError && <p className="error-text">{buildContextError}</p>}

              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={closeBuildContext} disabled={buildContextBusy}>Close</button>
                <button type="submit" className="btn-primary" disabled={buildContextBusy || !buildContextPrompt.trim() || !buildContextProviderId}>
                  {buildContextBusy ? 'Building...' : 'Build and Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {crawlEvents.length > 0 && (
        <CrawlStatusPanel events={crawlEvents} />
      )}

      <div className="card" style={{ marginBottom: '16px' }}>
        <table>
          <tbody>
            <tr><td title="Unique identifier for this database entry" style={{ fontWeight: 500, width: '140px' }}>ID</td><td>{detail.DatabaseId}</td></tr>
            <tr><td title="Database engine type" style={{ fontWeight: 500 }}>Type</td><td>{detail.Type}</td></tr>
            <tr><td title="Database schema name" style={{ fontWeight: 500 }}>Schema</td><td>{detail.Schema || '-'}</td></tr>
            <tr>
              <td title="Whether the schema has been successfully crawled" style={{ fontWeight: 500 }}>Status</td>
              <td>
                {detail.IsCrawled
                  ? <span className="badge badge-success" title="Schema geometry has been successfully discovered">Crawled</span>
                  : <span className="badge badge-warning" title="Schema crawl has not completed - geometry may be unavailable">Degraded</span>
                }
              </td>
            </tr>
            {detail.CrawledUtc && <tr><td title="Timestamp of the last successful crawl" style={{ fontWeight: 500 }}>Last Crawl</td><td>{new Date(detail.CrawledUtc).toLocaleString()}</td></tr>}
            {detail.CrawlError && <tr><td title="Error from the last crawl attempt" style={{ fontWeight: 500 }}>Error</td><td className="error-text">{detail.CrawlError}</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card" style={{ marginBottom: '16px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px', marginBottom: '10px' }}>
          <h3 title="User-supplied description of the database for AI agents" style={{ fontSize: '14px', margin: 0, color: 'var(--text-secondary)' }}>Context</h3>
          {!editingContext && (
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <ClipboardButton text={detail.Context || ''} title="Copy database context" label="Copy database context" />
              <button className="btn-secondary" title="Edit the context stored in server settings" onClick={handleStartContextEdit}>Edit Context</button>
            </div>
          )}
        </div>

        {editingContext ? (
          <>
            <textarea
              title="Context stored for this database"
              rows={8}
              value={contextDraft}
              onChange={event => setContextDraft(event.target.value)}
              placeholder="Describe the database, important tables, declared relationships, inferred relationships, and common query patterns."
              style={{ fontFamily: 'var(--font-mono)', fontSize: '13px', marginBottom: '10px' }}
            />
            {contextError && <p className="error-text" style={{ marginBottom: '10px' }}>{contextError}</p>}
            <div style={{ display: 'flex', gap: '8px' }}>
              <button className="btn-primary" title="Save context to server settings" onClick={handleSaveContext} disabled={savingContext}>
                {savingContext ? 'Saving...' : 'Save Context'}
              </button>
              <button className="btn-secondary" title="Discard context edits" onClick={handleCancelContextEdit} disabled={savingContext}>Cancel</button>
            </div>
          </>
        ) : (
          <>
            {detail.Context
              ? <pre className="database-context-display">{detail.Context}</pre>
              : <p className="muted-text" style={{ marginBottom: contextMessage ? '8px' : 0 }}>No context saved.</p>
            }
            {contextMessage && <p className="muted-text" style={{ margin: 0 }}>{contextMessage}</p>}
          </>
        )}
      </div>

      {detail.Tables.length > 0 && (
        <div>
          <h3 title="Tables discovered by the schema crawler" style={{ fontSize: '16px', marginBottom: '12px' }}>Tables ({detail.Tables.length})</h3>
          {detail.Tables.map(table => (
            <div key={table.TableName} className="card" style={{ marginBottom: '12px' }}>
              <h4 title="Fully qualified table name" style={{ fontSize: '14px', marginBottom: '8px' }}>{table.SchemaName}.{table.TableName}</h4>
              <table>
                <thead>
                  <tr>
                    <th title="Column name">Column</th>
                    <th title="Column data type">Type</th>
                    <th title="Primary key">PK</th>
                    <th title="Whether the column accepts null values">Nullable</th>
                    <th title="Default value expression">Default</th>
                  </tr>
                </thead>
                <tbody>
                  {table.Columns.map(col => (
                    <tr key={col.ColumnName} title={col.ColumnName + ' (' + col.DataType + ')'}>
                      <td>{col.ColumnName}</td>
                      <td style={{ fontFamily: 'var(--font-mono)', fontSize: '13px' }}>{col.DataType}</td>
                      <td>{col.IsPrimaryKey ? 'Yes' : ''}</td>
                      <td>{col.IsNullable ? 'Yes' : ''}</td>
                      <td className="muted-text">{col.DefaultValue || ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {table.ForeignKeys.length > 0 && (
                <div style={{ marginTop: '8px' }}>
                  <span className="muted-text" title="Foreign key relationships from this table to other tables" style={{ fontSize: '12px' }}>
                    FK: {table.ForeignKeys.map(fk => `${fk.ColumnName} -> ${fk.ReferencedTable}.${fk.ReferencedColumn}`).join(', ')}
                  </span>
                </div>
              )}

              {table.Indexes.length > 0 && (
                <div style={{ marginTop: '4px' }}>
                  <span className="muted-text" title="Indexes defined on this table" style={{ fontSize: '12px' }}>
                    Indexes: {table.Indexes.map(idx => `${idx.IndexName} (${idx.Columns.join(', ')}${idx.IsUnique ? ', unique' : ''})`).join(', ')}
                  </span>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function CrawlStatusPanel({ events }: { events: CrawlProgressEvent[] }) {
  const logRef = useRef<HTMLDivElement | null>(null);
  const latest = events[events.length - 1];
  const percent = Math.max(0, Math.min(100, latest?.Percent ?? 0));
  const failed = latest?.EventType === 'failed';
  const completed = latest?.EventType === 'completed';

  useEffect(() => {
    if (logRef.current) {
      logRef.current.scrollTop = logRef.current.scrollHeight;
    }
  }, [events.length]);

  return (
    <div className="card" style={{ marginBottom: '16px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px', marginBottom: '10px' }}>
        <div>
          <h3 title="Current schema crawl status" style={{ fontSize: '15px', marginBottom: '2px' }}>Crawl Status</h3>
          <p className={failed ? 'error-text' : 'muted-text'} title={latest?.Stage || 'crawl status'} style={{ margin: 0 }}>
            {latest?.Message || 'Preparing crawl.'}
          </p>
        </div>
        <span className={failed ? 'badge badge-danger' : completed ? 'badge badge-success' : 'badge badge-warning'} title="Latest crawl event state">
          {failed ? 'Failed' : completed ? 'Complete' : 'Running'}
        </span>
      </div>

      <div title={`${percent}% complete`} style={{ height: '8px', backgroundColor: 'var(--bg-tertiary)', borderRadius: '999px', overflow: 'hidden', marginBottom: '12px' }}>
        <div style={{ width: `${percent}%`, height: '100%', backgroundColor: failed ? 'var(--danger)' : 'var(--accent)', transition: 'width 0.2s ease' }} />
      </div>

      <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap', marginBottom: latest?.Error ? '10px' : '12px' }}>
        <span className="muted-text" title="Elapsed crawl time">Elapsed: {formatDuration(latest?.TotalMs || 0)}</span>
        {latest?.TableName && <span className="muted-text" title="Table currently examined">Current table: {latest.TableName}</span>}
        {latest?.TableIndex != null && latest?.TableCount != null && <span className="muted-text" title="Table progress">Table {latest.TableIndex} of {latest.TableCount}</span>}
        {latest?.TableCount != null && <span className="muted-text" title="Tables discovered">Tables: {latest.TableCount}</span>}
        {latest?.RelationshipCount != null && <span className="muted-text" title="Declared relationships discovered">Relationships: {latest.RelationshipCount}</span>}
      </div>

      {latest?.Error && (
        <p className="error-text" title="Crawl error" style={{ marginBottom: '12px' }}>{latest.Error}</p>
      )}

      <div className="crawl-event-log" ref={logRef}>
        {events.map((event, index) => (
          <div key={`${event.Stage}-${index}`} style={{ display: 'flex', gap: '8px', alignItems: 'baseline', marginBottom: '4px' }}>
            <span className="muted-text" title="Crawl progress percent" style={{ width: '42px', fontFamily: 'var(--font-mono)', fontSize: '12px' }}>{event.Percent}%</span>
            <span title={event.Stage} style={{ fontSize: '13px' }}>
              {event.Message}
              {event.TableIndex != null && event.TableCount != null && ` (${event.TableIndex}/${event.TableCount})`}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

function formatDuration(totalMs: number) {
  if (!Number.isFinite(totalMs) || totalMs <= 0) return '0 ms';
  if (totalMs < 1000) return `${Math.round(totalMs)} ms`;
  return `${(totalMs / 1000).toFixed(2)} s`;
}

function defaultContextPrompt(detail: DatabaseDetail) {
  const name = detail.Name || detail.DatabaseName || detail.Filename || detail.DatabaseId;
  return `Analyze the last successful crawl for ${name} and produce concise, durable context for future database chat and MCP usage.

Include:
- The likely purpose of the database when inferable from table and column names.
- Major entities and workflow groupings.
- Declared relationships and clearly labeled inferred relationships.
- Important naming conventions, key columns, and join paths.
- Safe query guidance that respects the configured allowed query types.

Do not include credentials, secrets, raw result rows, or unrelated commentary.`;
}

function CloseIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}

function BackIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="m12 19-7-7 7-7" />
      <path d="M19 12H5" />
    </svg>
  );
}
