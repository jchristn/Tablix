import { useEffect, useState } from 'react';
import { apiFetch } from '../api/client';
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

const initialProvider: ModelProviderUpdate = {
  Id: 'provider_ollama_local',
  Name: 'Local Ollama',
  Type: 'Ollama',
  Endpoint: 'http://ollama:11434',
  ApiKey: '',
  HasApiKey: false,
  Model: 'gemma3:4b',
  SystemPrompt: null,
  Enabled: true,
  DefaultStreaming: true,
  SupportsNativeToolCalls: true,
  UseNativeToolCalls: false,
  SupportsStrictJson: false,
  ToolCapabilityNote: 'Enable native tools after validating this provider/model emits tool calls reliably.',
  Temperature: 0.2,
  TopP: null,
  MaxTokens: 4096,
  RequestTimeoutMs: 120000,
  ClearApiKey: false,
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
  AllowedQueries: ['SELECT', 'INSERT', 'UPDATE', 'DELETE'],
  Context: '',
};

export default function SetupWizard() {
  const [visible, setVisible] = useState(false);
  const [stepIndex, setStepIndex] = useState(0);
  const [provider, setProvider] = useState<ModelProviderUpdate>({ ...initialProvider });
  const [database, setDatabase] = useState<DatabaseEntry>({ ...initialDatabase });
  const [allowedQueries, setAllowedQueries] = useState('SELECT, INSERT, UPDATE, DELETE');
  const [detail, setDetail] = useState<DatabaseDetail | null>(null);
  const [providerTest, setProviderTest] = useState<ProviderConnectivityTestResponse | null>(null);
  const [databaseTest, setDatabaseTest] = useState<DatabaseConnectivityTestResponse | null>(null);
  const [crawlEvents, setCrawlEvents] = useState<CrawlProgressEvent[]>([]);
  const [tableContexts, setTableContexts] = useState<Record<string, string>>({});
  const [tableContextPrompt, setTableContextPrompt] = useState(defaultTableContextPrompt());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => { loadSetupState(); }, []);

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
    setBusy(true);
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
      const candidate = { ...database, AllowedQueries: parseAllowedQueries() };
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
      const candidate = { ...database, AllowedQueries: parseAllowedQueries() };
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
      const response = await apiFetch(`/v1/database/${database.Id}/table-context/build`, {
        method: 'POST',
        body: JSON.stringify({
          ProviderId: provider.Id,
          Prompt: tableContextPrompt,
          TableIds: tableIds,
        }),
      });
      const result: BuildTableContextResponse = await response.json();
      if (!response.ok || !result.Success) throw new Error(result.Error || 'Table context generation failed.');

      const generated: Record<string, string> = {};
      result.Objects.forEach(context => {
        if (context.TableId) generated[context.TableId] = context.Context || '';
      });
      setTableContexts(previous => ({ ...previous, ...generated }));

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

  async function completeSetup() {
    await apiFetch('/v1/setup/complete', { method: 'POST' });
    setVisible(false);
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

  function parseAllowedQueries() {
    return allowedQueries.split(',').map(query => query.trim()).filter(Boolean);
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
        </div>

        <div className="setup-stepper">
          {steps.map((name, index) => <span key={name} className={index <= stepIndex ? 'active' : ''}>{index + 1}</span>)}
        </div>

        {step === 'provider' && (
          <section className="setup-body">
            <h4>Model Provider</h4>
            <div className="settings-grid">
              <Field label="Provider ID"><input value={provider.Id} onChange={event => setProvider(previous => ({ ...previous, Id: event.target.value }))} /></Field>
              <Field label="Name"><input value={provider.Name || ''} onChange={event => setProvider(previous => ({ ...previous, Name: event.target.value }))} /></Field>
              <Field label="Type">
                <select value={provider.Type} onChange={event => setProvider(previous => ({ ...previous, Type: event.target.value }))}>
                  <option value="Ollama">Ollama</option>
                  <option value="OpenAI">OpenAI</option>
                  <option value="OpenAICompatible">OpenAI Compatible</option>
                  <option value="Gemini">Gemini</option>
                </select>
              </Field>
              <Field label="Endpoint"><input value={provider.Endpoint || ''} onChange={event => setProvider(previous => ({ ...previous, Endpoint: event.target.value }))} /></Field>
              <Field label="Model"><input value={provider.Model || ''} onChange={event => setProvider(previous => ({ ...previous, Model: event.target.value }))} /></Field>
              <Field label="API Key"><input type="password" value={provider.ApiKey || ''} onChange={event => setProvider(previous => ({ ...previous, ApiKey: event.target.value }))} /></Field>
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
                <select value={database.Type} onChange={event => setDatabase(previous => ({ ...previous, Type: event.target.value }))}>
                  <option value="Sqlite">SQLite</option>
                  <option value="Postgresql">PostgreSQL</option>
                  <option value="Mysql">MySQL</option>
                  <option value="SqlServer">SQL Server</option>
                </select>
              </Field>
              <Field label="Filename"><input value={database.Filename || ''} onChange={event => setDatabase(previous => ({ ...previous, Filename: event.target.value }))} /></Field>
              <Field label="Database Name"><input value={database.DatabaseName || ''} onChange={event => setDatabase(previous => ({ ...previous, DatabaseName: event.target.value }))} /></Field>
              <Field label="Schema"><input value={database.Schema || ''} onChange={event => setDatabase(previous => ({ ...previous, Schema: event.target.value }))} /></Field>
              <Field label="Allowed Queries"><input value={allowedQueries} onChange={event => setAllowedQueries(event.target.value)} /></Field>
            </div>
            <TestResult result={databaseTest} />
          </section>
        )}

        {step === 'crawl' && (
          <section className="setup-body">
            <h4>Crawl Database</h4>
            <div className="crawl-event-log setup-log">
              {crawlEvents.map((event, index) => <div key={`${event.Stage}-${index}`}>{event.Percent}% {event.Message}</div>)}
            </div>
          </section>
        )}

        {step === 'database-context' && (
          <section className="setup-body">
            <h4>Database Context</h4>
            <p className="muted-text">Generate durable context from the latest crawl with the selected provider.</p>
          </section>
        )}

        {step === 'table-context' && (
          <section className="setup-body">
            <h4>Table Context</h4>
            <div className="form-group">
              <label>Generation Instructions</label>
              <textarea rows={4} value={tableContextPrompt} onChange={event => setTableContextPrompt(event.target.value)} />
            </div>
            <div className="setup-table-contexts">
              {(detail?.Tables || []).map(table => (
                <div key={table.TableId || table.TableName} className="setup-table-context">
                  <label>{table.SchemaName}.{table.TableName}</label>
                  <textarea rows={3} value={table.TableId ? tableContexts[table.TableId] || '' : ''} onChange={event => table.TableId && setTableContexts(previous => ({ ...previous, [table.TableId as string]: event.target.value }))} />
                </div>
              ))}
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
          {step === 'provider' && <button className="btn-secondary" onClick={testProvider} disabled={busy}>Test Provider</button>}
          {step === 'provider' && <button className="btn-primary" onClick={saveProviderAndContinue} disabled={busy}>Save and Continue</button>}
          {step === 'database' && <button className="btn-secondary" onClick={testDatabase} disabled={busy}>Test Database</button>}
          {step === 'database' && <button className="btn-primary" onClick={saveDatabaseAndContinue} disabled={busy}>Save and Continue</button>}
          {step === 'crawl' && <button className="btn-primary" onClick={crawlDatabase} disabled={busy}>{busy ? 'Crawling...' : 'Start Crawl'}</button>}
          {step === 'database-context' && <button className="btn-primary" onClick={buildDatabaseContext} disabled={busy}>{busy ? 'Building...' : 'Build Database Context'}</button>}
          {step === 'table-context' && <button className="btn-secondary" onClick={saveTableContextsAndContinue} disabled={busy}>Save Edited Contexts</button>}
          {step === 'table-context' && <button className="btn-primary" onClick={buildTableContextsAndContinue} disabled={busy || !tableContextPrompt.trim()}>{busy ? 'Building...' : 'Build Table Contexts'}</button>}
          {step === 'complete' && <button className="btn-primary" onClick={completeSetup}>Go to Chat When Ready</button>}
        </div>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="form-group"><label>{label}</label>{children}</div>;
}

function TestResult({ result }: { result: ProviderConnectivityTestResponse | DatabaseConnectivityTestResponse | null }) {
  if (!result) return null;
  return <p className={result.Success ? 'muted-text' : 'error-text'}>{result.Message || result.Error}</p>;
}

function parseSseData(frame: string) {
  const lines = frame.replace(/\r/g, '').split('\n');
  const data: string[] = [];
  lines.forEach(line => {
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
  });
  return data.join('\n');
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
