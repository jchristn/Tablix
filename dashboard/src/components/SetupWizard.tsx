import { useEffect, useRef, useState } from 'react';
import { apiFetch } from '../api/client';
import { translateTooltip } from '../i18n';
import type {
  BuildContextResponse,
  BuildTableContextResponse,
  CrawlProgressEvent,
  DatabaseConnectivityTestResponse,
  DatabaseDetail,
  DatabaseEntry,
  ModelProviderUpdate,
  ProviderConnectivityTestResponse,
  SetupStateRead,
} from '../types';

const steps = ['provider', 'database', 'crawl', 'database-context', 'table-context', 'complete'];
const allowedQueryOptions = ['SELECT', 'INSERT', 'UPDATE', 'DELETE', 'WITH', 'CALL'];
const networkDefaults: Record<string, { Port: number; User: string; Schema: string }> = {
  Postgresql: { Port: 5432, User: 'postgres', Schema: 'public' },
  Mysql: { Port: 3306, User: 'root', Schema: '' },
  SqlServer: { Port: 1433, User: 'sa', Schema: 'dbo' },
};

const initialDatabase: DatabaseEntry = {
  Id: 'db_sample_sqlite',
  Name: 'Sample E-Commerce',
  Type: 'Sqlite',
  Hostname: '',
  Port: null,
  User: '',
  Password: '',
  DatabaseName: 'sample',
  Schema: 'main',
  Filename: './database.db',
  AllowedQueries: [...allowedQueryOptions],
  Context: '',
};

export default function SetupWizard() {
  const [visible, setVisible] = useState(false);
  const [stepIndex, setStepIndex] = useState(0);
  const [provider, setProvider] = useState<ModelProviderUpdate>(createProviderDefaults('Ollama'));
  const [database, setDatabase] = useState<DatabaseEntry>({ ...initialDatabase });
  const [detail, setDetail] = useState<DatabaseDetail | null>(null);
  const [providerTest, setProviderTest] = useState<ProviderConnectivityTestResponse | null>(null);
  const [databaseTest, setDatabaseTest] = useState<DatabaseConnectivityTestResponse | null>(null);
  const [crawlEvents, setCrawlEvents] = useState<CrawlProgressEvent[]>([]);
  const [tableContexts, setTableContexts] = useState<Record<string, string>>({});
  const [tableContextPrompt, setTableContextPrompt] = useState(defaultTableContextPrompt());
  const [busy, setBusy] = useState(false);
  const [providerTesting, setProviderTesting] = useState(false);
  const [error, setError] = useState('');
  const crawlLogRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => { loadSetupState(); }, []);

  useEffect(() => {
    const log = crawlLogRef.current;
    if (!log) return;

    requestAnimationFrame(() => {
      log.scrollTop = log.scrollHeight;
    });
  }, [crawlEvents]);

  async function loadSetupState() {
    try {
      const response = await apiFetch('/v1/setup');
      if (!response.ok) return;
      const state: SetupStateRead = await response.json();
      setVisible(state.ShouldShowWizard);
    } catch {
      setVisible(false);
    }
  }

  async function updateSetup(currentStep: string, selectedProviderId?: string, selectedDatabaseId?: string) {
    await apiFetch('/v1/setup', {
      method: 'PUT',
      body: JSON.stringify({
        Status: 'InProgress',
        CurrentStep: currentStep,
        SelectedProviderId: selectedProviderId || provider.Id,
        SelectedDatabaseId: selectedDatabaseId || database.Id,
      }),
    });
  }

  async function testProvider() {
    if (providerTesting) return;

    setBusy(true);
    setProviderTesting(true);
    setError('');
    try {
      const response = await apiFetch('/v1/model/test', {
        method: 'POST',
        body: JSON.stringify({ Provider: provider }),
      });
      const result: ProviderConnectivityTestResponse = await response.json();
      setProviderTest(result);
    } catch {
      setProviderTest({ Success: false, ProviderId: provider.Id, Model: provider.Model, Message: null, Error: 'Could not connect to server.', TotalMs: 0 });
    } finally {
      setProviderTesting(false);
      setBusy(false);
    }
  }

  async function saveProviderAndContinue() {
    setBusy(true);
    setError('');
    try {
      const create = await apiFetch('/v1/model', { method: 'POST', body: JSON.stringify(provider) });
      if (!create.ok) {
        const update = await apiFetch(`/v1/model/${provider.Id}`, { method: 'PUT', body: JSON.stringify(provider) });
        if (!update.ok) throw new Error('Failed to save provider.');
      }

      await updateSetup('database', provider.Id, database.Id);
      setStepIndex(1);
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Failed to save provider.');
    } finally {
      setBusy(false);
    }
  }

  async function testDatabase() {
    setBusy(true);
    setError('');
    try {
      const candidate = buildDatabaseCandidate();
      const response = await apiFetch('/v1/database/test', {
        method: 'POST',
        body: JSON.stringify({ Database: candidate }),
      });
      const result: DatabaseConnectivityTestResponse = await response.json();
      setDatabaseTest(result);
    } catch {
      setDatabaseTest({ Success: false, DatabaseId: database.Id, Message: null, Error: 'Could not connect to server.', TotalMs: 0 });
    } finally {
      setBusy(false);
    }
  }

  async function saveDatabaseAndContinue() {
    setBusy(true);
    setError('');
    try {
      const candidate = buildDatabaseCandidate();
      const create = await apiFetch('/v1/database', { method: 'POST', body: JSON.stringify(candidate) });
      if (!create.ok) {
        const update = await apiFetch(`/v1/database/${candidate.Id}`, { method: 'PUT', body: JSON.stringify(candidate) });
        if (!update.ok) throw new Error('Failed to save database.');
      }

      await updateSetup('crawl', provider.Id, candidate.Id);
      setStepIndex(2);
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Failed to save database.');
    } finally {
      setBusy(false);
    }
  }

  async function crawlDatabase() {
    setBusy(true);
    setError('');
    setCrawlEvents([]);
    try {
      const response = await apiFetch(`/v1/database/${database.Id}/crawl/stream`, { method: 'POST' });
      if (!response.body) throw new Error('Crawl stream was unavailable.');

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let done = false;
      while (!done) {
        const chunk = await reader.read();
        done = chunk.done;
        if (chunk.value) {
          buffer += decoder.decode(chunk.value, { stream: !done });
          const frames = buffer.split('\n\n');
          buffer = frames.pop() || '';
          frames.forEach(handleCrawlFrame);
        }
      }
      if (buffer.trim()) handleCrawlFrame(buffer);

      await updateSetup('database-context', provider.Id, database.Id);
      setStepIndex(3);
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Crawl failed.');
    } finally {
      setBusy(false);
    }
  }

  async function buildDatabaseContext() {
    setBusy(true);
    setError('');
    try {
      const response = await apiFetch(`/v1/database/${database.Id}/context/build`, {
        method: 'POST',
        body: JSON.stringify({ ProviderId: provider.Id, Prompt: defaultContextPrompt() }),
      });
      const result: BuildContextResponse = await response.json();
      if (!response.ok || !result.Success) throw new Error(result.Error || 'Context generation failed.');

      const detailResponse = await apiFetch(`/v1/database/${database.Id}`);
      const latest: DatabaseDetail = await detailResponse.json();
      setDetail(latest);
      setTableContexts(createTableContextDrafts(latest));
      await updateSetup('table-context', provider.Id, database.Id);
      setStepIndex(4);
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Context generation failed.');
    } finally {
      setBusy(false);
    }
  }

  async function saveTableContextsAndContinue() {
    if (!detail) return;
    setBusy(true);
    setError('');
    try {
      for (const table of detail.Tables) {
        if (!table.TableId) continue;
        await apiFetch(`/v1/database/${database.Id}/table-context/${table.TableId}`, {
          method: 'PUT',
          body: JSON.stringify({
            Context: tableContexts[table.TableId] || null,
            Mode: 'replace',
            Source: 'user',
          }),
        });
      }

      await updateSetup('complete', provider.Id, database.Id);
      setStepIndex(5);
    } catch {
      setError('Failed to save table contexts.');
    } finally {
      setBusy(false);
    }
  }

  async function buildTableContextsAndContinue() {
    if (!detail) return;
    setBusy(true);
    setError('');
    try {
      const tableIds = detail.Tables.map(table => table.TableId).filter((tableId): tableId is string => Boolean(tableId));
      await buildTableContextsWithConcurrency(tableIds);

      const detailResponse = await apiFetch(`/v1/database/${database.Id}`);
      const latest: DatabaseDetail = await detailResponse.json();
      setDetail(latest);
      setTableContexts(createTableContextDrafts(latest));
      await updateSetup('complete', provider.Id, database.Id);
      setStepIndex(5);
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Table context generation failed.');
    } finally {
      setBusy(false);
    }
  }

  async function buildTableContextsWithConcurrency(tableIds: string[]) {
    const concurrency = clampConcurrency(provider.MaxConcurrentRequests);
    let nextIndex = 0;

    async function worker() {
      while (nextIndex < tableIds.length) {
        const tableId = tableIds[nextIndex];
        nextIndex++;
        const response = await apiFetch(`/v1/database/${database.Id}/table-context/${tableId}/build`, {
          method: 'POST',
          body: JSON.stringify({
            ProviderId: provider.Id,
            Prompt: tableContextPrompt,
            TableIds: [tableId],
          }),
        });
        const result: BuildTableContextResponse = await readJsonResponse(response, 'Table context generation failed.');
        if (!response.ok || !result.Success) throw new Error(result.Error || 'Table context generation failed.');
        const received: Record<string, string> = {};
        result.Objects.forEach(context => {
          if (context.TableId) received[context.TableId] = context.Context || '';
        });
        setTableContexts(previous => ({ ...previous, ...received }));
      }
    }

    const workers: Promise<void>[] = [];
    const workerCount = Math.min(concurrency, Math.max(tableIds.length, 1));
    for (let index = 0; index < workerCount; index++) {
      workers.push(worker());
    }

    await Promise.all(workers);
  }

  async function completeSetup() {
    await apiFetch('/v1/setup/complete', { method: 'POST' });
    setVisible(false);
  }

  async function dismissSetup() {
    setVisible(false);
    try {
      await apiFetch('/v1/setup/dismiss', { method: 'POST' });
    } catch {
    }
  }

  function handleCrawlFrame(frame: string) {
    const data = parseSseData(frame);
    if (!data) return;
    const event: CrawlProgressEvent = JSON.parse(data);
    setCrawlEvents(previous => [...previous, event]);
    if (event.Detail) {
      setDetail(event.Detail);
      setTableContexts(createTableContextDrafts(event.Detail));
    }
  }

  function updateDatabaseType(type: string) {
    setDatabase(previous => {
      if (type === 'Sqlite') {
        return {
          ...previous,
          Type: type,
          Hostname: '',
          Port: null,
          User: '',
          Password: '',
          Schema: 'main',
          Filename: './database.db',
        };
      }

      const defaults = networkDefaults[type] || { Port: 0, User: '', Schema: '' };
      return {
        ...previous,
        Type: type,
        Hostname: previous.Hostname || 'localhost',
        Port: defaults.Port,
        User: defaults.User,
        Schema: defaults.Schema,
        Filename: '',
      };
    });
  }

  function toggleAllowedQuery(query: string) {
    setDatabase(previous => {
      const existing = previous.AllowedQueries || [];
      const next = existing.includes(query)
        ? existing.filter(item => item !== query)
        : [...existing, query];
      return { ...previous, AllowedQueries: next };
    });
  }

  function updateProviderSupportsNativeTools(supportsNativeTools: boolean) {
    setProvider(previous => ({
      ...previous,
      SupportsNativeToolCalls: supportsNativeTools,
      UseNativeToolCalls: supportsNativeTools ? true : false,
    }));
  }

  function updateProviderType(type: string) {
    setProvider(createProviderDefaults(type));
    setProviderTest(null);
    setError('');
  }

  function buildDatabaseCandidate(): DatabaseEntry {
    if (database.Type === 'Sqlite') {
      return {
        ...database,
        Hostname: '',
        Port: null,
        User: '',
        Password: '',
        Filename: database.Filename || './database.db',
        AllowedQueries: database.AllowedQueries || [],
      };
    }

    return {
      ...database,
      Filename: '',
      Port: database.Port || null,
      AllowedQueries: database.AllowedQueries || [],
    };
  }

  if (!visible) return null;

  const step = steps[stepIndex];

  return (
    <div className="modal-backdrop setup-backdrop" role="presentation">
      <div className="modal-panel setup-wizard" role="dialog" aria-modal="true" aria-labelledby="setup-title">
        <div className="modal-header">
          <div>
            <h3 id="setup-title">Set Up Tablix</h3>
            <p className="muted-text">Step {stepIndex + 1} of {steps.length}</p>
          </div>
          <button type="button" className="icon-action" aria-label="Exit setup wizard" title="Exit setup wizard" onClick={dismissSetup}>
            <CloseIcon />
          </button>
        </div>

        <div className="setup-stepper">
          {steps.map((name, index) => <span key={name} className={index <= stepIndex ? 'active' : ''}>{index + 1}</span>)}
        </div>

        {step === 'provider' && (
          <section className="setup-body">
            <h4>Model Provider</h4>
            <div className="settings-grid">
              <Field label="Provider ID" tooltipKey="models.id"><input title={translateTooltip('models.id')} value={provider.Id} onChange={event => setProvider(previous => ({ ...previous, Id: event.target.value }))} /></Field>
              <Field label="Name" tooltipKey="models.name"><input title={translateTooltip('models.name')} value={provider.Name || ''} onChange={event => setProvider(previous => ({ ...previous, Name: event.target.value }))} /></Field>
              <Field label="Type">
                <select title={translateTooltip('models.type')} value={provider.Type} onChange={event => updateProviderType(event.target.value)}>
                  <option value="Ollama">Ollama</option>
                  <option value="OpenAI">OpenAI</option>
                  <option value="OpenAICompatible">OpenAI Compatible</option>
                  <option value="Gemini">Gemini</option>
                </select>
              </Field>
              <Field label="Endpoint" tooltipKey="models.endpoint"><input title={translateTooltip('models.endpoint')} value={provider.Endpoint || ''} onChange={event => setProvider(previous => ({ ...previous, Endpoint: event.target.value }))} /></Field>
              <Field label="Model" tooltipKey="models.model"><input title={translateTooltip('models.model')} value={provider.Model || ''} onChange={event => setProvider(previous => ({ ...previous, Model: event.target.value }))} /></Field>
              <Field label="API Key" tooltipKey="models.apiKey"><input title={translateTooltip('models.apiKey')} type="password" value={provider.ApiKey || ''} onChange={event => setProvider(previous => ({ ...previous, ApiKey: event.target.value }))} /></Field>
              <Field label="Max Concurrent Requests" tooltipKey="models.concurrency"><input title={translateTooltip('models.concurrency')} type="number" min="1" max="16" value={provider.MaxConcurrentRequests} onChange={event => setProvider(previous => ({ ...previous, MaxConcurrentRequests: parseBoundedNumber(event.target.value, 1, 1, 16) }))} /></Field>
            </div>
            <div className="setup-provider-toggles">
              <label className="toggle-row settings-toggle" title={translateTooltip('models.supportsTools')}><input title={translateTooltip('models.supportsTools')} type="checkbox" checked={provider.SupportsNativeToolCalls} onChange={event => updateProviderSupportsNativeTools(event.target.checked)} /><span>Supports native tools</span></label>
              <label className="toggle-row settings-toggle" title={translateTooltip('models.useTools')}><input title={translateTooltip('models.useTools')} type="checkbox" checked={provider.UseNativeToolCalls} disabled={!provider.SupportsNativeToolCalls} onChange={event => setProvider(previous => ({ ...previous, UseNativeToolCalls: event.target.checked }))} /><span>Use native tools</span></label>
            </div>
            <TestResult result={providerTest} />
          </section>
        )}

        {step === 'database' && (
          <section className="setup-body">
            <h4>Database</h4>
            <div className="settings-grid">
              <Field label="Database ID"><input value={database.Id} onChange={event => setDatabase(previous => ({ ...previous, Id: event.target.value }))} /></Field>
              <Field label="Name"><input value={database.Name || ''} onChange={event => setDatabase(previous => ({ ...previous, Name: event.target.value }))} /></Field>
              <Field label="Type">
                <select value={database.Type} onChange={event => updateDatabaseType(event.target.value)}>
                  <option value="Sqlite">SQLite</option>
                  <option value="Postgresql">PostgreSQL</option>
                  <option value="Mysql">MySQL</option>
                  <option value="SqlServer">SQL Server</option>
                </select>
              </Field>
              {database.Type === 'Sqlite' ? (
                <Field label="Filename"><input value={database.Filename || ''} onChange={event => setDatabase(previous => ({ ...previous, Filename: event.target.value }))} placeholder="./database.db" /></Field>
              ) : (
                <>
                  <Field label="Hostname"><input value={database.Hostname || ''} onChange={event => setDatabase(previous => ({ ...previous, Hostname: event.target.value }))} placeholder="localhost" /></Field>
                  <Field label="Port"><input type="number" value={database.Port ?? ''} onChange={event => setDatabase(previous => ({ ...previous, Port: parseOptionalPort(event.target.value) }))} /></Field>
                  <Field label="User"><input value={database.User || ''} onChange={event => setDatabase(previous => ({ ...previous, User: event.target.value }))} /></Field>
                  <Field label="Password"><input type="password" value={database.Password || ''} onChange={event => setDatabase(previous => ({ ...previous, Password: event.target.value }))} /></Field>
                  <Field label="Database Name"><input value={database.DatabaseName || ''} onChange={event => setDatabase(previous => ({ ...previous, DatabaseName: event.target.value }))} /></Field>
                  {database.Type !== 'Mysql' && (
                    <Field label="Schema"><input value={database.Schema || ''} onChange={event => setDatabase(previous => ({ ...previous, Schema: event.target.value }))} placeholder={database.Type === 'SqlServer' ? 'dbo' : 'public'} /></Field>
                  )}
                </>
              )}
              {database.Type === 'Sqlite' && (
                <>
                  <Field label="Database Name"><input value={database.DatabaseName || ''} onChange={event => setDatabase(previous => ({ ...previous, DatabaseName: event.target.value }))} /></Field>
                  <Field label="Schema"><input value={database.Schema || ''} onChange={event => setDatabase(previous => ({ ...previous, Schema: event.target.value }))} placeholder="main" /></Field>
                </>
              )}
            </div>
            <div className="form-group">
              <label>Allowed Queries</label>
              <div className="allowed-query-options">
                {allowedQueryOptions.map(query => (
                  <label key={query} className="checkbox-option">
                    <input type="checkbox" checked={(database.AllowedQueries || []).includes(query)} onChange={() => toggleAllowedQuery(query)} />
                    <span>{query}</span>
                  </label>
                ))}
              </div>
            </div>
            <TestResult result={databaseTest} />
          </section>
        )}

        {step === 'crawl' && (
          <section className="setup-body">
            <h4>Crawl Database</h4>
            <div className="crawl-event-log setup-log" ref={crawlLogRef}>
              {crawlEvents.map((event, index) => <div key={`${event.Stage}-${index}`}>{event.Percent}% {event.Message}</div>)}
            </div>
          </section>
        )}

        {step === 'database-context' && (
          <section className="setup-body">
            <h4>Database Context</h4>
            <p className="muted-text">Generate durable context from the latest crawl with the selected provider.</p>
            <p className="muted-text">This operation may take some time, please be patient.</p>
          </section>
        )}

        {step === 'table-context' && (
          <section className="setup-body">
            <h4>Table Context</h4>
            <div className="form-group">
              <label>Generation Instructions</label>
              <textarea rows={4} value={tableContextPrompt} onChange={event => setTableContextPrompt(event.target.value)} />
            </div>
            <div className="setup-table-contexts" role="region" aria-label="Table contexts">
              <table className="setup-table-context-table">
                <thead>
                  <tr>
                    <th>Table</th>
                    <th>Context</th>
                  </tr>
                </thead>
                <tbody>
                  {(detail?.Tables || []).map(table => (
                    <tr key={table.TableId || table.TableName}>
                      <td className="setup-table-context-name">
                        <span>{table.SchemaName}</span>
                        <strong>{table.TableName}</strong>
                      </td>
                      <td>
                        <textarea rows={2} value={table.TableId ? tableContexts[table.TableId] || '' : ''} onChange={event => table.TableId && setTableContexts(previous => ({ ...previous, [table.TableId as string]: event.target.value }))} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {step === 'complete' && (
          <section className="setup-body">
            <h4>Ready for Chat</h4>
            <p className="muted-text">Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.</p>
          </section>
        )}

        {error && <p className="error-text">{error}</p>}

        <div className="modal-actions">
          {step !== 'complete' && <button type="button" className="btn-secondary setup-skip-button" onClick={dismissSetup}>Skip setup</button>}
          {step === 'provider' && <button type="button" className="btn-secondary" onClick={testProvider} disabled={busy || providerTesting} aria-disabled={busy || providerTesting}>Test Provider</button>}
          {step === 'provider' && <button className="btn-primary" onClick={saveProviderAndContinue} disabled={busy}>Save and Continue</button>}
          {step === 'database' && <button className="btn-secondary" onClick={testDatabase} disabled={busy}>Test Database</button>}
          {step === 'database' && <button className="btn-primary" onClick={saveDatabaseAndContinue} disabled={busy}>Save and Continue</button>}
          {step === 'crawl' && <button className="btn-primary" onClick={crawlDatabase} disabled={busy}>{busy ? 'Crawling...' : 'Start Crawl'}</button>}
          {step === 'database-context' && <button className="btn-primary" onClick={buildDatabaseContext} disabled={busy}>{busy ? 'Building...' : 'Build Database Context'}</button>}
          {step === 'table-context' && <button className="btn-secondary" onClick={saveTableContextsAndContinue} disabled={busy}>Save Edited Contexts</button>}
          {step === 'table-context' && <button className="btn-primary" onClick={buildTableContextsAndContinue} disabled={busy || !tableContextPrompt.trim()}>{busy ? 'Building...' : 'Build Table Contexts'}</button>}
          {step === 'complete' && <button className="btn-primary" onClick={completeSetup}>Go to Chat When Ready</button>}
        </div>

        {providerTesting && (
          <div className="modal-backdrop setup-validation-backdrop" role="presentation">
            <div className="modal-panel setup-validation-modal" role="dialog" aria-modal="true" aria-labelledby="provider-test-title" aria-busy="true">
              <div className="setup-spinner" aria-hidden="true" />
              <div>
                <h4 id="provider-test-title">Testing Provider</h4>
                <p className="muted-text">Tablix is validating that the model endpoint can be reached and can answer a small request.</p>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function Field({ label, tooltipKey, children }: { label: string; tooltipKey?: string; children: React.ReactNode }) {
  return <div className="form-group"><label title={tooltipKey ? translateTooltip(tooltipKey) : undefined}>{label}</label>{children}</div>;
}

function createProviderDefaults(type: string): ModelProviderUpdate {
  const common = {
    ApiKey: '',
    HasApiKey: false,
    SystemPrompt: null,
    Enabled: true,
    DefaultStreaming: true,
    SupportsNativeToolCalls: true,
    UseNativeToolCalls: true,
    ToolCapabilityNote: null,
    Temperature: 0.2,
    TopP: null,
    MaxTokens: 4096,
    RequestTimeoutMs: 120000,
    ClearApiKey: false,
  };

  if (type === 'OpenAI') {
    return {
      ...common,
      Id: 'provider_openai',
      Name: 'OpenAI',
      Type: 'OpenAI',
      Endpoint: 'https://api.openai.com',
      Model: 'gpt-4o-mini',
      SupportsStrictJson: true,
      MaxConcurrentRequests: 4,
    };
  }

  if (type === 'OpenAICompatible') {
    return {
      ...common,
      Id: 'provider_openai_compatible',
      Name: 'OpenAI Compatible',
      Type: 'OpenAICompatible',
      Endpoint: 'http://localhost:1234',
      Model: 'local-model',
      SupportsStrictJson: false,
      MaxConcurrentRequests: 1,
    };
  }

  if (type === 'Gemini') {
    return {
      ...common,
      Id: 'provider_gemini',
      Name: 'Gemini',
      Type: 'Gemini',
      Endpoint: 'https://generativelanguage.googleapis.com',
      Model: 'gemini-2.5-flash',
      SupportsStrictJson: true,
      MaxConcurrentRequests: 4,
    };
  }

  return {
    ...common,
    Id: 'provider_ollama_local',
    Name: 'Local Ollama',
    Type: 'Ollama',
    Endpoint: 'http://ollama:11434',
    Model: 'gemma3:4b',
    SupportsStrictJson: false,
    MaxConcurrentRequests: 1,
  };
}

function TestResult({ result }: { result: ProviderConnectivityTestResponse | DatabaseConnectivityTestResponse | null }) {
  if (!result) return null;
  return <p className={result.Success ? 'muted-text' : 'error-text'}>{result.Message || result.Error}</p>;
}

function CloseIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}

function parseSseData(frame: string) {
  const lines = frame.replace(/\r/g, '').split('\n');
  const data: string[] = [];
  lines.forEach(line => {
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
  });
  return data.join('\n');
}

function parseOptionalPort(value: string) {
  if (!value.trim()) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? null : parsed;
}

function parseBoundedNumber(value: string, fallback: number, min: number, max: number) {
  const parsed = Number.parseInt(value, 10);
  if (Number.isNaN(parsed)) return fallback;
  return Math.min(Math.max(parsed, min), max);
}

function clampConcurrency(value: number) {
  return Math.min(Math.max(value || 1, 1), 16);
}

async function readJsonResponse<T>(response: Response, fallback: string): Promise<T> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.toLowerCase().includes('application/json')) {
    return await response.json();
  }

  const text = await response.text();
  const stripped = text.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
  const status = response.status > 0 ? `${response.status} ${response.statusText}`.trim() : 'request failed';
  throw new Error(stripped ? `${fallback} ${status}: ${stripped}` : `${fallback} ${status}.`);
}

function createTableContextDrafts(detail: DatabaseDetail) {
  const drafts: Record<string, string> = {};
  detail.Tables.forEach(table => {
    if (table.TableId) drafts[table.TableId] = table.Context || '';
  });
  return drafts;
}

function defaultContextPrompt() {
  return 'Analyze the crawled schema and produce concise durable context for future database chat and MCP usage. Include major entities, relationships, naming conventions, key columns, and safe query guidance. Clearly label inferred relationships and do not include secrets or raw data.';
}

function defaultTableContextPrompt() {
  return 'For each selected table, produce concise durable context describing the table purpose, key columns, primary keys, relationships and join paths, common filters, caveats, and how it is typically used. Clearly label inferred relationships and do not include secrets or raw data.';
}
