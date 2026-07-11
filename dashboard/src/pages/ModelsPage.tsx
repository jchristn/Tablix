import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type {
  EnumerationResult,
  ModelProviderRead,
  ModelProviderSummary,
  ModelProviderUpdate,
  ProviderConnectivityTestResponse,
} from '../types';

const providerTypes = ['Ollama', 'OpenAI', 'OpenAICompatible', 'Gemini'];

const emptyProvider: ModelProviderUpdate = {
  Id: '',
  Name: '',
  Type: 'Ollama',
  Endpoint: 'http://localhost:11434',
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

export default function ModelsPage() {
  const navigate = useNavigate();
  const [models, setModels] = useState<ModelProviderSummary[]>([]);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [provider, setProvider] = useState<ModelProviderUpdate>({ ...emptyProvider });
  const [testResult, setTestResult] = useState<ProviderConnectivityTestResponse | null>(null);

  useEffect(() => { loadModels(); }, []);

  async function loadModels() {
    try {
      const response = await apiFetch('/v1/model?maxResults=1000&skip=0');
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to load models.'); return; }

      const data: EnumerationResult<ModelProviderSummary> = await response.json();
      setModels(data.Objects || []);
      setError('');
    } catch {
      setError('Could not connect to server.');
    }
  }

  function openCreate() {
    setProvider({ ...emptyProvider, Id: `provider_${crypto.randomUUID().slice(0, 8)}` });
    setEditing(false);
    setTestResult(null);
    setModalOpen(true);
  }

  async function openEdit(id: string) {
    const response = await apiFetch(`/v1/model/${id}`);
    if (response.status === 401) { navigate('/login'); return; }
    if (!response.ok) { setError('Failed to load provider.'); return; }

    const data: ModelProviderRead = await response.json();
    setProvider({ ...data, ApiKey: '', ClearApiKey: false });
    setEditing(true);
    setTestResult(null);
    setModalOpen(true);
  }

  async function saveProvider(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError('');
    setMessage('');

    try {
      const response = editing
        ? await apiFetch(`/v1/model/${provider.Id}`, { method: 'PUT', body: JSON.stringify(provider) })
        : await apiFetch('/v1/model', { method: 'POST', body: JSON.stringify(provider) });

      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        const data = await response.json().catch(() => null);
        setError(data?.Description || data?.Message || 'Failed to save provider.');
        return;
      }

      setMessage('Model provider saved.');
      setModalOpen(false);
      await loadModels();
    } catch {
      setError('Could not connect to server.');
    } finally {
      setBusy(false);
    }
  }

  async function deleteProvider(id: string) {
    if (!confirm('Delete this model provider?')) return;
    const response = await apiFetch(`/v1/model/${id}`, { method: 'DELETE' });
    if (response.status === 401) { navigate('/login'); return; }
    if (!response.ok) { setError('Failed to delete provider.'); return; }
    setMessage('Model provider deleted.');
    await loadModels();
  }

  async function testProvider(id?: string) {
    setBusy(true);
    setTestResult(null);
    try {
      const response = id
        ? await apiFetch(`/v1/model/${id}/test`, { method: 'POST' })
        : await apiFetch('/v1/model/test', { method: 'POST', body: JSON.stringify({ Provider: provider }) });

      if (response.status === 401) { navigate('/login'); return; }
      const data: ProviderConnectivityTestResponse = await response.json();
      setTestResult(data);
      if (!id && data.Success) setMessage('Provider test succeeded.');
    } catch {
      setTestResult({ Success: false, ProviderId: provider.Id, Model: provider.Model, Message: null, Error: 'Could not connect to server.', TotalMs: 0 });
    } finally {
      setBusy(false);
    }
  }

  function update(field: keyof ModelProviderUpdate, value: string | number | boolean | null) {
    setProvider(previous => ({ ...previous, [field]: value }));
  }

  return (
    <div>
      <div className="page-header">
        <h2 title="Manage model endpoints used by Chat and context generation">Models</h2>
        <button className="btn-primary" onClick={openCreate}>Add Model</button>
      </div>

      {error && <p className="error-text" style={{ marginBottom: '12px' }}>{error}</p>}
      {message && <p className="muted-text" style={{ marginBottom: '12px' }}>{message}</p>}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Type</th>
              <th>Endpoint</th>
              <th>Model</th>
              <th>Tools</th>
              <th>Status</th>
              <th style={{ width: '170px' }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {models.map(model => (
              <tr key={model.Id}>
                <td>
                  <strong>{model.Name || model.Id}</strong>
                  <div className="muted-text">{model.Id}</div>
                </td>
                <td>{model.Type}</td>
                <td className="muted-text">{model.Endpoint || '-'}</td>
                <td>{model.Model || '-'}</td>
                <td>{model.UseNativeToolCalls ? 'Native' : model.SupportsNativeToolCalls ? 'Available' : 'Fallback'}</td>
                <td>{model.Enabled ? <span className="badge badge-success">Enabled</span> : <span className="badge badge-warning">Disabled</span>}</td>
                <td>
                  <div className="row-actions-inline">
                    <button className="btn-secondary compact-button" onClick={() => openEdit(model.Id)}>Edit</button>
                    <button className="btn-secondary compact-button" onClick={() => testProvider(model.Id)} disabled={busy}>Test</button>
                    <button className="btn-danger compact-button" onClick={() => deleteProvider(model.Id)}>Delete</button>
                  </div>
                </td>
              </tr>
            ))}
            {models.length === 0 && (
              <tr><td colSpan={7} className="muted-text">No model providers configured.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {testResult && !modalOpen && (
        <div className="card" style={{ marginTop: '16px' }}>
          <strong>{testResult.Success ? 'Provider test succeeded' : 'Provider test failed'}</strong>
          <p className={testResult.Success ? 'muted-text' : 'error-text'}>{testResult.Message || testResult.Error}</p>
        </div>
      )}

      {modalOpen && (
        <div className="modal-backdrop" role="presentation" onClick={() => !busy && setModalOpen(false)}>
          <div className="modal-panel model-modal" role="dialog" aria-modal="true" aria-labelledby="model-modal-title" onClick={event => event.stopPropagation()}>
            <div className="modal-header">
              <div>
                <h3 id="model-modal-title">{editing ? 'Edit Model' : 'Add Model'}</h3>
                <p className="muted-text">{provider.Id}</p>
              </div>
              <button type="button" className="icon-action" aria-label="Close model dialog" title="Close" onClick={() => setModalOpen(false)} disabled={busy}>
                <CloseIcon />
              </button>
            </div>

            <form onSubmit={saveProvider} className="model-form">
              <div className="settings-grid">
                <Field label="Id"><input value={provider.Id} onChange={event => update('Id', event.target.value)} disabled={editing} /></Field>
                <Field label="Name"><input value={provider.Name || ''} onChange={event => update('Name', event.target.value)} /></Field>
                <Field label="Type">
                  <select value={provider.Type} onChange={event => update('Type', event.target.value)}>
                    {providerTypes.map(type => <option key={type} value={type}>{type}</option>)}
                  </select>
                </Field>
                <Field label="Endpoint"><input value={provider.Endpoint || ''} onChange={event => update('Endpoint', event.target.value)} /></Field>
                <Field label="Model"><input value={provider.Model || ''} onChange={event => update('Model', event.target.value)} /></Field>
                <Field label={provider.HasApiKey ? 'API Key Configured' : 'API Key'}>
                  <input type="password" value={provider.ApiKey || ''} placeholder={provider.HasApiKey ? 'Leave blank to keep existing key' : ''} onChange={event => update('ApiKey', event.target.value)} />
                </Field>
                <label className="toggle-row settings-toggle"><input type="checkbox" checked={provider.Enabled} onChange={event => update('Enabled', event.target.checked)} /><span>Enabled</span></label>
                <label className="toggle-row settings-toggle"><input type="checkbox" checked={provider.DefaultStreaming} onChange={event => update('DefaultStreaming', event.target.checked)} /><span>Streaming</span></label>
                <label className="toggle-row settings-toggle"><input type="checkbox" checked={provider.SupportsNativeToolCalls} onChange={event => update('SupportsNativeToolCalls', event.target.checked)} /><span>Supports native tools</span></label>
                <label className="toggle-row settings-toggle"><input type="checkbox" checked={provider.UseNativeToolCalls} onChange={event => update('UseNativeToolCalls', event.target.checked)} /><span>Use native tools</span></label>
                <label className="toggle-row settings-toggle"><input type="checkbox" checked={provider.SupportsStrictJson} onChange={event => update('SupportsStrictJson', event.target.checked)} /><span>Strict JSON</span></label>
                <label className="toggle-row settings-toggle"><input type="checkbox" checked={provider.ClearApiKey || false} onChange={event => update('ClearApiKey', event.target.checked)} /><span>Clear API key</span></label>
                <Field label="Temperature"><input type="number" step="0.1" value={provider.Temperature ?? ''} onChange={event => update('Temperature', nullableNumber(event.target.value))} /></Field>
                <Field label="Top P"><input type="number" step="0.1" value={provider.TopP ?? ''} onChange={event => update('TopP', nullableNumber(event.target.value))} /></Field>
                <Field label="Max Tokens"><input type="number" value={provider.MaxTokens ?? ''} onChange={event => update('MaxTokens', nullableNumber(event.target.value))} /></Field>
                <Field label="Timeout Ms"><input type="number" value={provider.RequestTimeoutMs} onChange={event => update('RequestTimeoutMs', parseNumber(event.target.value, 120000))} /></Field>
              </div>
              <div className="form-group" style={{ marginTop: '12px' }}>
                <label>Tool Capability Note</label>
                <textarea rows={2} value={provider.ToolCapabilityNote || ''} onChange={event => update('ToolCapabilityNote', event.target.value)} />
              </div>
              <div className="form-group">
                <label>System Prompt Override</label>
                <textarea rows={3} value={provider.SystemPrompt || ''} onChange={event => update('SystemPrompt', event.target.value)} />
              </div>

              {testResult && (
                <p className={testResult.Success ? 'muted-text' : 'error-text'}>{testResult.Message || testResult.Error}</p>
              )}

              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setModalOpen(false)} disabled={busy}>Cancel</button>
                <button type="button" className="btn-secondary" onClick={() => testProvider()} disabled={busy}>Test</button>
                <button type="submit" className="btn-primary" disabled={busy}>{busy ? 'Saving...' : 'Save'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="form-group">
      <label>{label}</label>
      {children}
    </div>
  );
}

function CloseIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}

function parseNumber(value: string, fallback: number) {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function nullableNumber(value: string) {
  if (value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
