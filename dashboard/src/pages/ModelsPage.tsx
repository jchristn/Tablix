import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiFetch } from '../api/client';
import ActionMenu, { EllipsisIcon, openActionMenuFromButton, type ActionMenuState } from '../components/ActionMenu';
import ConfirmDialog from '../components/ConfirmDialog';
import { isInteractiveRowClick } from '../components/RecordViewModal';
import { translateTooltip } from '../i18n';
import type {
  EndpointHealthStatus,
  EnumerationResult,
  HealthCheckRecord,
  ModelProviderRead,
  ModelProviderSummary,
  ModelProviderUpdate,
  ProviderConnectivityTestResponse,
} from '../types';

const providerTypes = ['Ollama', 'OpenAI', 'OpenAICompatible', 'Gemini'];
const healthCheckMethods = ['GET', 'HEAD'];

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
  UseNativeToolCalls: true,
  SupportsStrictJson: false,
  ToolCapabilityNote: null,
  Temperature: 0.2,
  TopP: null,
  MaxTokens: 4096,
  RequestTimeoutMs: 120000,
  MaxConcurrentRequests: 1,
  HealthCheckEnabled: true,
  HealthCheckUrl: 'http://localhost:11434/api/tags',
  HealthCheckMethod: 'GET',
  HealthCheckIntervalMs: 5000,
  HealthCheckTimeoutMs: 2000,
  HealthCheckExpectedStatusCode: 200,
  HealthyThreshold: 2,
  UnhealthyThreshold: 2,
  HealthCheckUseAuth: false,
  Health: null,
  ClearApiKey: false,
};

export default function ModelsPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const routedProviderIdRef = useRef<string | null>(null);
  const [models, setModels] = useState<ModelProviderSummary[]>([]);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [provider, setProvider] = useState<ModelProviderUpdate>({ ...emptyProvider });
  const [testResult, setTestResult] = useState<ProviderConnectivityTestResponse | null>(null);
  const [actionMenu, setActionMenu] = useState<ModelActionMenuState | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ModelProviderSummary | null>(null);
  const [healthTarget, setHealthTarget] = useState<ModelProviderSummary | null>(null);

  useEffect(() => { loadModels(); }, []);
  useEffect(() => {
    const routedProviderId = searchParams.get('provider');
    if (!routedProviderId) {
      routedProviderIdRef.current = null;
      return;
    }

    if (routedProviderIdRef.current === routedProviderId || models.length === 0) return;
    if (!models.some(model => model.Id === routedProviderId)) return;

    routedProviderIdRef.current = routedProviderId;
    async function openRoutedProvider(providerId: string) {
      const response = await apiFetch(`/v1/model/${providerId}`);
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to load provider.'); return; }

      const data: ModelProviderRead = await response.json();
      setProvider(normalizeProviderRead(data));
      setEditing(true);
      setTestResult(null);
      setModalOpen(true);
    }

    void openRoutedProvider(routedProviderId);
  }, [models, navigate, searchParams]);

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
    setProvider({ ...createModelProviderDefaults('Ollama'), Id: `provider_${crypto.randomUUID().slice(0, 8)}` });
    setEditing(false);
    setTestResult(null);
    setModalOpen(true);
  }

  async function openEdit(id: string) {
    const response = await apiFetch(`/v1/model/${id}`);
    if (response.status === 401) { navigate('/login'); return; }
    if (!response.ok) { setError('Failed to load provider.'); return; }

    const data: ModelProviderRead = await response.json();
    setProvider(normalizeProviderRead(data));
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
      const body = JSON.stringify(providerPayload(provider));
      const response = editing
        ? await apiFetch(`/v1/model/${provider.Id}`, { method: 'PUT', body })
        : await apiFetch('/v1/model', { method: 'POST', body });

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

  function requestDeleteProvider(model: ModelProviderSummary) {
    setActionMenu(null);
    setDeleteTarget(model);
  }

  async function deleteProvider() {
    if (!deleteTarget) return;
    const id = deleteTarget.Id;
    setBusy(true);
    setError('');
    try {
      const response = await apiFetch(`/v1/model/${id}`, { method: 'DELETE' });
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        setError('Failed to delete provider.');
        return;
      }
      setMessage('Model provider deleted.');
      setDeleteTarget(null);
      await loadModels();
    } catch {
      setError('Could not connect to server.');
    } finally {
      setBusy(false);
    }
  }

  async function testProvider(id?: string) {
    setBusy(true);
    setTestResult(null);
    try {
      const response = id
        ? await apiFetch(`/v1/model/${id}/test`, { method: 'POST' })
        : await apiFetch('/v1/model/test', { method: 'POST', body: JSON.stringify({ Provider: providerPayload(provider) }) });

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
    setProvider(previous => {
      if (field === 'SupportsNativeToolCalls') {
        const supports = Boolean(value);
        return { ...previous, SupportsNativeToolCalls: supports, UseNativeToolCalls: supports ? true : false };
      }

      if (field === 'Type') {
        const type = String(value);
        const endpoint = previous.Endpoint || defaultProviderEndpoint(type);
        return {
          ...previous,
          Type: type,
          Endpoint: endpoint,
          Model: previous.Model || defaultProviderModel(type),
          SupportsNativeToolCalls: true,
          UseNativeToolCalls: true,
          SupportsStrictJson: type === 'OpenAI' || type === 'Gemini',
          MaxConcurrentRequests: type === 'OpenAI' || type === 'Gemini' ? 4 : 1,
          HealthCheckUrl: defaultHealthCheckUrl(type, endpoint),
          HealthCheckUseAuth: type === 'OpenAI' || type === 'Gemini',
        };
      }

      if (field === 'Endpoint') {
        const endpoint = String(value);
        const currentDefaultUrl = defaultHealthCheckUrl(previous.Type, previous.Endpoint || '');
        const shouldUpdateHealthUrl = !previous.HealthCheckUrl || previous.HealthCheckUrl === currentDefaultUrl;
        return {
          ...previous,
          Endpoint: endpoint,
          HealthCheckUrl: shouldUpdateHealthUrl ? defaultHealthCheckUrl(previous.Type, endpoint) : previous.HealthCheckUrl,
        };
      }

      return { ...previous, [field]: value };
    });
  }

  function openActionMenu(model: ModelProviderSummary, event: React.MouseEvent<HTMLButtonElement>) {
    event.preventDefault();
    event.stopPropagation();
    setActionMenu({ ...openActionMenuFromButton(event.currentTarget), Model: model });
  }

  function openModelRow(model: ModelProviderSummary, event: React.MouseEvent<HTMLTableRowElement>) {
    if (isInteractiveRowClick(event)) return;
    openEdit(model.Id);
  }

  function openHealthDetails(model: ModelProviderSummary, event: React.MouseEvent<HTMLElement>) {
    event.preventDefault();
    event.stopPropagation();
    setHealthTarget(model);
  }

  return (
    <div>
      <div className="page-header">
        <h2 title={translateTooltip('nav.models')}>Models</h2>
        <button className="btn-primary" title={translateTooltip('models.add')} onClick={openCreate}>Add Model</button>
      </div>

      {error && <p className="error-text" style={{ marginBottom: '12px' }}>{error}</p>}
      {message && <p className="muted-text" style={{ marginBottom: '12px' }}>{message}</p>}

      <div className="card">
        <table className="data-table wide-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Type</th>
              <th>Endpoint</th>
              <th>Model</th>
              <th>Tools</th>
              <th>Health</th>
              <th>Status</th>
              <th className="actions-column">Actions</th>
            </tr>
          </thead>
          <tbody>
            {models.map(model => {
              const health = modelHealthPresentation(model, model.Health);
              return (
                <tr key={model.Id} title="Click to edit model provider" style={{ cursor: 'pointer' }} onClick={event => openModelRow(model, event)}>
                  <td>
                    <strong>{model.Name || model.Id}</strong>
                    <div className="muted-text">{model.Id}</div>
                  </td>
                  <td>{model.Type}</td>
                  <td className="muted-text">{model.Endpoint || '-'}</td>
                  <td>{model.Model || '-'}</td>
                  <td>{model.UseNativeToolCalls ? 'Native' : model.SupportsNativeToolCalls ? 'Available' : 'Fallback'}</td>
                  <td className="model-health-cell">
                    <div className="model-health-inline">
                      <button
                        type="button"
                        className={`health-status-button ${health.Tone}`}
                        title={`${health.Label}: ${health.Title}`}
                        aria-label={`${health.Label}: ${health.Title}`}
                        onClick={event => openHealthDetails(model, event)}
                      >
                        <HealthStatusIcon Tone={health.Tone} />
                      </button>
                      <HealthHistogram
                        History={model.Health?.History || []}
                        Compact={true}
                        Label={`Open health details for ${model.Name || model.Id}`}
                        OnOpenDetails={event => openHealthDetails(model, event)}
                      />
                    </div>
                  </td>
                  <td>{model.Enabled ? <span className="badge badge-success">Enabled</span> : <span className="badge badge-warning">Disabled</span>}</td>
                  <td>
                    <button
                      type="button"
                      className="icon-action row-actions-button"
                      title={translateTooltip('actions.open')}
                      aria-label={`Open actions for ${model.Id}`}
                      onClick={event => openActionMenu(model, event)}
                    >
                      <EllipsisIcon />
                    </button>
                  </td>
                </tr>
              );
            })}
            {models.length === 0 && (
              <tr><td colSpan={8} className="muted-text">No model providers configured.</td></tr>
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

      <ActionMenu
        State={actionMenu}
        OnClose={() => setActionMenu(null)}
        Items={actionMenu ? [
          {
            Label: 'Edit',
            TooltipKey: 'actions.edit',
            OnClick: () => openEdit(actionMenu.Model.Id)
          },
          {
            Label: 'Health',
            TooltipKey: 'actions.details',
            OnClick: () => setHealthTarget(actionMenu.Model)
          },
          {
            Label: 'Test',
            TooltipKey: 'actions.test',
            Disabled: busy,
            OnClick: () => testProvider(actionMenu.Model.Id)
          },
          {
            Label: 'Delete',
            TooltipKey: 'actions.delete',
            Danger: true,
            OnClick: () => requestDeleteProvider(actionMenu.Model)
          }
        ] : []}
      />

      <ConfirmDialog
        Open={deleteTarget != null}
        Title="Delete Model Provider"
        Message={`Delete model provider '${deleteTarget?.Name || deleteTarget?.Id || ''}'? Chat and context generation can no longer use it.`}
        ConfirmLabel="Delete"
        Busy={busy}
        Danger={true}
        OnConfirm={deleteProvider}
        OnCancel={() => !busy && setDeleteTarget(null)}
      />

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
                <Field label="Id" tooltipKey="models.id"><input title={translateTooltip('models.id')} value={provider.Id} onChange={event => update('Id', event.target.value)} disabled={editing} /></Field>
                <Field label="Name" tooltipKey="models.name"><input title={translateTooltip('models.name')} value={provider.Name || ''} onChange={event => update('Name', event.target.value)} /></Field>
                <Field label="Type">
                  <select title={translateTooltip('models.type')} value={provider.Type} onChange={event => update('Type', event.target.value)}>
                    {providerTypes.map(type => <option key={type} value={type}>{type}</option>)}
                  </select>
                </Field>
                <Field label="Endpoint" tooltipKey="models.endpoint"><input title={translateTooltip('models.endpoint')} value={provider.Endpoint || ''} onChange={event => update('Endpoint', event.target.value)} /></Field>
                <Field label="Model" tooltipKey="models.model"><input title={translateTooltip('models.model')} value={provider.Model || ''} onChange={event => update('Model', event.target.value)} /></Field>
                <Field label={provider.HasApiKey ? 'API Key Configured' : 'API Key'} tooltipKey="models.apiKey">
                  <input title={translateTooltip('models.apiKey')} type="password" value={provider.ApiKey || ''} placeholder={provider.HasApiKey ? 'Leave blank to keep existing key' : ''} onChange={event => update('ApiKey', event.target.value)} />
                </Field>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.enabled')}><input title={translateTooltip('models.enabled')} type="checkbox" checked={provider.Enabled} onChange={event => update('Enabled', event.target.checked)} /><span>Enabled</span></label>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.streaming')}><input title={translateTooltip('models.streaming')} type="checkbox" checked={provider.DefaultStreaming} onChange={event => update('DefaultStreaming', event.target.checked)} /><span>Streaming</span></label>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.supportsTools')}><input title={translateTooltip('models.supportsTools')} type="checkbox" checked={provider.SupportsNativeToolCalls} onChange={event => update('SupportsNativeToolCalls', event.target.checked)} /><span>Supports native tools</span></label>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.useTools')}><input title={translateTooltip('models.useTools')} type="checkbox" checked={provider.UseNativeToolCalls} onChange={event => update('UseNativeToolCalls', event.target.checked)} disabled={!provider.SupportsNativeToolCalls} /><span>Use native tools</span></label>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.strictJson')}><input title={translateTooltip('models.strictJson')} type="checkbox" checked={provider.SupportsStrictJson} onChange={event => update('SupportsStrictJson', event.target.checked)} /><span>Strict JSON</span></label>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.clearKey')}><input title={translateTooltip('models.clearKey')} type="checkbox" checked={provider.ClearApiKey || false} onChange={event => update('ClearApiKey', event.target.checked)} /><span>Clear API key</span></label>
                <Field label="Temperature" tooltipKey="models.temperature"><input title={translateTooltip('models.temperature')} type="number" step="0.1" value={provider.Temperature ?? ''} onChange={event => update('Temperature', nullableNumber(event.target.value))} /></Field>
                <Field label="Top P" tooltipKey="models.topP"><input title={translateTooltip('models.topP')} type="number" step="0.1" value={provider.TopP ?? ''} onChange={event => update('TopP', nullableNumber(event.target.value))} /></Field>
                <Field label="Max Tokens" tooltipKey="models.maxTokens"><input title={translateTooltip('models.maxTokens')} type="number" value={provider.MaxTokens ?? ''} onChange={event => update('MaxTokens', nullableNumber(event.target.value))} /></Field>
                <Field label="Timeout Ms" tooltipKey="models.timeout"><input title={translateTooltip('models.timeout')} type="number" value={provider.RequestTimeoutMs} onChange={event => update('RequestTimeoutMs', parseNumber(event.target.value, 120000))} /></Field>
                <Field label="Max Concurrent Requests" tooltipKey="models.concurrency"><input title={translateTooltip('models.concurrency')} type="number" min="1" max="16" value={provider.MaxConcurrentRequests} onChange={event => update('MaxConcurrentRequests', parseNumber(event.target.value, 1))} /></Field>
              </div>
              <div className="model-form-section">Health Checks</div>
              <div className="settings-grid">
                <label className="toggle-row settings-toggle" title={translateTooltip('models.healthEnabled')}><input title={translateTooltip('models.healthEnabled')} type="checkbox" checked={provider.HealthCheckEnabled} onChange={event => update('HealthCheckEnabled', event.target.checked)} /><span>Health checks</span></label>
                <label className="toggle-row settings-toggle" title={translateTooltip('models.healthAuth')}><input title={translateTooltip('models.healthAuth')} type="checkbox" checked={provider.HealthCheckUseAuth} onChange={event => update('HealthCheckUseAuth', event.target.checked)} /><span>Use API key</span></label>
                <Field label="Health URL" tooltipKey="models.healthUrl"><input title={translateTooltip('models.healthUrl')} value={provider.HealthCheckUrl || ''} onChange={event => update('HealthCheckUrl', event.target.value)} /></Field>
                <Field label="Method" tooltipKey="models.healthMethod">
                  <select title={translateTooltip('models.healthMethod')} value={provider.HealthCheckMethod || 'GET'} onChange={event => update('HealthCheckMethod', event.target.value)}>
                    {healthCheckMethods.map(method => <option key={method} value={method}>{method}</option>)}
                  </select>
                </Field>
                <Field label="Interval Ms" tooltipKey="models.healthInterval"><input title={translateTooltip('models.healthInterval')} type="number" min="1000" value={provider.HealthCheckIntervalMs} onChange={event => update('HealthCheckIntervalMs', parseNumber(event.target.value, 5000))} /></Field>
                <Field label="Timeout Ms" tooltipKey="models.healthTimeout"><input title={translateTooltip('models.healthTimeout')} type="number" min="10" value={provider.HealthCheckTimeoutMs} onChange={event => update('HealthCheckTimeoutMs', parseNumber(event.target.value, 2000))} /></Field>
                <Field label="Expected Status" tooltipKey="models.healthStatus"><input title={translateTooltip('models.healthStatus')} type="number" min="100" max="599" value={provider.HealthCheckExpectedStatusCode} onChange={event => update('HealthCheckExpectedStatusCode', parseNumber(event.target.value, 200))} /></Field>
                <Field label="Healthy Threshold" tooltipKey="models.healthyThreshold"><input title={translateTooltip('models.healthyThreshold')} type="number" min="1" max="100" value={provider.HealthyThreshold} onChange={event => update('HealthyThreshold', parseNumber(event.target.value, 2))} /></Field>
                <Field label="Unhealthy Threshold" tooltipKey="models.unhealthyThreshold"><input title={translateTooltip('models.unhealthyThreshold')} type="number" min="1" max="100" value={provider.UnhealthyThreshold} onChange={event => update('UnhealthyThreshold', parseNumber(event.target.value, 2))} /></Field>
              </div>
              <div className="form-group">
                <label title={translateTooltip('models.systemPrompt')}>System Prompt Override</label>
                <textarea title={translateTooltip('models.systemPrompt')} rows={3} value={provider.SystemPrompt || ''} onChange={event => update('SystemPrompt', event.target.value)} />
              </div>

              {testResult && (
                <p className={testResult.Success ? 'muted-text' : 'error-text'}>{testResult.Message || testResult.Error}</p>
              )}

              <div className="modal-actions">
                <button type="button" className="btn-secondary" title={translateTooltip('models.cancel')} onClick={() => setModalOpen(false)} disabled={busy}>Cancel</button>
                <button type="button" className="btn-secondary" title={translateTooltip('models.modalTest')} onClick={() => testProvider()} disabled={busy}>Test</button>
                <button type="submit" className="btn-primary" title={translateTooltip('models.save')} disabled={busy}>{busy ? 'Saving...' : 'Save'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {healthTarget && (
        <ModelProviderHealthModal
          provider={healthTarget}
          onClose={() => setHealthTarget(null)}
          onUnauthorized={() => navigate('/login')}
        />
      )}
    </div>
  );
}

interface ModelActionMenuState extends ActionMenuState {
  Model: ModelProviderSummary;
}

type HealthTone = 'success' | 'warning' | 'danger' | 'neutral';

interface HealthPresentation {
  Label: string;
  Title: string;
  Tone: HealthTone;
}

function ModelProviderHealthModal({ provider, onClose, onUnauthorized }: { provider: ModelProviderSummary; onClose: () => void; onUnauthorized: () => void }) {
  const [health, setHealth] = useState<EndpointHealthStatus | null>(provider.Health || null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let canceled = false;

    async function loadHealth() {
      setLoading(true);
      try {
        const response = await apiFetch(`/v1/model/${provider.Id}/health`);
        if (response.status === 401) { onUnauthorized(); return; }
        if (!response.ok) {
          if (!canceled) setError('Failed to load provider health.');
          return;
        }

        const data: EndpointHealthStatus = await response.json();
        if (!canceled) {
          setHealth(data);
          setError('');
        }
      } catch {
        if (!canceled) setError('Could not connect to server.');
      } finally {
        if (!canceled) setLoading(false);
      }
    }

    void loadHealth();
    const interval = window.setInterval(loadHealth, 15000);
    return () => {
      canceled = true;
      window.clearInterval(interval);
    };
  }, [provider.Id, onUnauthorized]);

  const presentation = modelHealthPresentation(provider, health);

  return (
    <div className="modal-backdrop" role="presentation" onClick={onClose}>
      <div className="modal-panel model-health-modal" role="dialog" aria-modal="true" aria-labelledby="model-health-title" onClick={event => event.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 id="model-health-title">Health: {provider.Name || provider.Id}</h3>
            <p className="muted-text">{provider.Id}</p>
          </div>
          <button type="button" className="icon-action" aria-label="Close health dialog" title="Close" onClick={onClose}>
            <CloseIcon />
          </button>
        </div>

        {error && <p className="error-text" style={{ marginBottom: '12px' }}>{error}</p>}
        {loading && !health && <p className="muted-text" style={{ marginBottom: '12px' }}>Loading health...</p>}

        <div className="health-stats-row">
          <HealthStatCard Label="Status" Value={presentation.Label} Tone={presentation.Tone} />
          <HealthStatCard Label="Uptime" Value={formatPercent(health?.UptimePercentage)} Tone={presentation.Tone === 'danger' ? 'danger' : 'success'} />
          <HealthStatCard Label="Consecutive OK" Value={String(health?.ConsecutiveSuccesses ?? 0)} Tone="success" />
          <HealthStatCard Label="Consecutive Fail" Value={String(health?.ConsecutiveFailures ?? 0)} Tone={health && health.ConsecutiveFailures > 0 ? 'danger' : 'neutral'} />
        </div>

        {health?.LastError && (
          <div className="health-error-box">
            <strong>Last Error</strong>
            <span>{health.LastError}</span>
          </div>
        )}

        <section className="health-histogram-section">
          <div className="health-section-label">Health History</div>
          <HealthHistogram History={health?.History || []} />
        </section>

        <div className="health-timestamps">
          <HealthKeyValue Label="First Check" Value={formatTimestamp(health?.FirstCheckUtc)} />
          <HealthKeyValue Label="Last Check" Value={formatTimestamp(health?.LastCheckUtc)} />
          <HealthKeyValue Label="Last Healthy" Value={formatTimestamp(health?.LastHealthyUtc)} />
          <HealthKeyValue Label="Last Unhealthy" Value={formatTimestamp(health?.LastUnhealthyUtc)} />
          <HealthKeyValue Label="Last State Change" Value={formatTimestamp(health?.LastStateChangeUtc)} />
          <HealthKeyValue Label="Uptime / Downtime" Value={`${formatDuration(health?.TotalUptimeMs || 0)} / ${formatDuration(health?.TotalDowntimeMs || 0)}`} />
        </div>

        <div className="health-section-label">Health Configuration</div>
        <table className="health-config-table">
          <tbody>
            <HealthConfigRow Label="Provider" Value={`${provider.Name || provider.Id} (${provider.Type})`} />
            <HealthConfigRow Label="Endpoint" Value={provider.Endpoint || '-'} />
            <HealthConfigRow Label="Health URL" Value={provider.HealthCheckUrl || '-'} />
            <HealthConfigRow Label="Method" Value={provider.HealthCheckMethod || 'GET'} />
            <HealthConfigRow Label="Interval" Value={formatDuration(provider.HealthCheckIntervalMs)} />
            <HealthConfigRow Label="Timeout" Value={formatDuration(provider.HealthCheckTimeoutMs)} />
            <HealthConfigRow Label="Expected Status" Value={String(provider.HealthCheckExpectedStatusCode || 200)} />
            <HealthConfigRow Label="Thresholds" Value={`${provider.HealthyThreshold || 2} healthy / ${provider.UnhealthyThreshold || 2} unhealthy`} />
            <HealthConfigRow Label="Uses Auth" Value={provider.HealthCheckUseAuth ? 'Yes' : 'No'} />
          </tbody>
        </table>
      </div>
    </div>
  );
}

function HealthStatCard({ Label, Value, Tone }: { Label: string; Value: string; Tone: HealthTone }) {
  return (
    <div className={`health-stat-card ${Tone}`}>
      <span>{Label}</span>
      <strong>{Value}</strong>
    </div>
  );
}

function HealthKeyValue({ Label, Value }: { Label: string; Value: string }) {
  return (
    <div className="health-timestamp-item">
      <span>{Label}</span>
      <strong>{Value}</strong>
    </div>
  );
}

function HealthConfigRow({ Label, Value }: { Label: string; Value: string }) {
  return (
    <tr>
      <th>{Label}</th>
      <td>{Value}</td>
    </tr>
  );
}

function HealthHistogram({
  History,
  Compact = false,
  Label = 'Health history',
  OnOpenDetails,
}: {
  History: HealthCheckRecord[];
  Compact?: boolean;
  Label?: string;
  OnOpenDetails?: (event: React.MouseEvent<HTMLButtonElement>) => void;
}) {
  const records = (History || [])
    .slice()
    .sort((left, right) => new Date(left.TimestampUtc).getTime() - new Date(right.TimestampUtc).getTime())
    .slice(Compact ? -10 : -72);
  const className = `health-histogram ${Compact ? 'compact' : ''}`;
  const title = records.length > 0
    ? `${records.length} recent health sample(s). Newest sample is on the right.`
    : 'No health history.';

  const content = records.length === 0
    ? <span className="health-histogram-empty">No data</span>
    : records.map((record, index) => (
        <span
          key={`${record.TimestampUtc}-${index}`}
          className={`health-bar ${record.Success ? 'success' : 'failure'}`}
          title={`${formatTimestamp(record.TimestampUtc)} - ${record.Success ? 'Success' : 'Failure'}`}
        />
      ));

  if (OnOpenDetails) {
    return (
      <button type="button" className={className} aria-label={Label} title={title} onClick={OnOpenDetails}>
        {content}
      </button>
    );
  }

  return (
    <div className={className} aria-label={Label} title={title}>
      {content}
    </div>
  );
}

function Field({ label, tooltipKey, children }: { label: string; tooltipKey?: string; children: React.ReactNode }) {
  return (
    <div className="form-group">
      <label title={tooltipKey ? translateTooltip(tooltipKey) : undefined}>{label}</label>
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

function HealthStatusIcon({ Tone }: { Tone: HealthTone }) {
  if (Tone === 'success') {
    return (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d="M20 6 9 17l-5-5" />
      </svg>
    );
  }

  if (Tone === 'danger') {
    return (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d="M18 6 6 18" />
        <path d="m6 6 12 12" />
      </svg>
    );
  }

  if (Tone === 'warning') {
    return (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v6" />
        <path d="M12 17h.01" />
      </svg>
    );
  }

  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M5 12h14" />
    </svg>
  );
}

function createModelProviderDefaults(type: string): ModelProviderUpdate {
  const endpoint = defaultProviderEndpoint(type);
  return {
    ...emptyProvider,
    Id: defaultProviderId(type),
    Name: defaultProviderName(type),
    Type: type,
    Endpoint: endpoint,
    Model: defaultProviderModel(type),
    SupportsStrictJson: type === 'OpenAI' || type === 'Gemini',
    MaxConcurrentRequests: type === 'OpenAI' || type === 'Gemini' ? 4 : 1,
    HealthCheckUrl: defaultHealthCheckUrl(type, endpoint),
    HealthCheckUseAuth: type === 'OpenAI' || type === 'Gemini',
    Health: null,
  };
}

function normalizeProviderRead(read: ModelProviderRead): ModelProviderUpdate {
  const endpoint = read.Endpoint || defaultProviderEndpoint(read.Type);
  return {
    ...emptyProvider,
    ...read,
    Endpoint: endpoint,
    ApiKey: '',
    ClearApiKey: false,
    HealthCheckEnabled: read.HealthCheckEnabled ?? true,
    HealthCheckUrl: read.HealthCheckUrl || defaultHealthCheckUrl(read.Type, endpoint),
    HealthCheckMethod: read.HealthCheckMethod || 'GET',
    HealthCheckIntervalMs: read.HealthCheckIntervalMs || 5000,
    HealthCheckTimeoutMs: read.HealthCheckTimeoutMs || 2000,
    HealthCheckExpectedStatusCode: read.HealthCheckExpectedStatusCode || 200,
    HealthyThreshold: read.HealthyThreshold || 2,
    UnhealthyThreshold: read.UnhealthyThreshold || 2,
    HealthCheckUseAuth: read.HealthCheckUseAuth ?? false,
    Health: read.Health || null,
  };
}

function providerPayload(provider: ModelProviderUpdate) {
  return {
    Id: provider.Id,
    Name: provider.Name,
    Type: provider.Type,
    Endpoint: provider.Endpoint,
    ApiKey: provider.ApiKey,
    ClearApiKey: provider.ClearApiKey,
    Model: provider.Model,
    SystemPrompt: provider.SystemPrompt,
    Enabled: provider.Enabled,
    DefaultStreaming: provider.DefaultStreaming,
    SupportsNativeToolCalls: provider.SupportsNativeToolCalls,
    UseNativeToolCalls: provider.UseNativeToolCalls,
    SupportsStrictJson: provider.SupportsStrictJson,
    ToolCapabilityNote: provider.ToolCapabilityNote,
    Temperature: provider.Temperature,
    TopP: provider.TopP,
    MaxTokens: provider.MaxTokens,
    RequestTimeoutMs: provider.RequestTimeoutMs,
    MaxConcurrentRequests: provider.MaxConcurrentRequests,
    HealthCheckEnabled: provider.HealthCheckEnabled,
    HealthCheckUrl: provider.HealthCheckUrl,
    HealthCheckMethod: provider.HealthCheckMethod,
    HealthCheckIntervalMs: provider.HealthCheckIntervalMs,
    HealthCheckTimeoutMs: provider.HealthCheckTimeoutMs,
    HealthCheckExpectedStatusCode: provider.HealthCheckExpectedStatusCode,
    HealthyThreshold: provider.HealthyThreshold,
    UnhealthyThreshold: provider.UnhealthyThreshold,
    HealthCheckUseAuth: provider.HealthCheckUseAuth,
  };
}

function modelHealthPresentation(provider: ModelProviderSummary, health?: EndpointHealthStatus | null): HealthPresentation {
  if (!provider.Enabled)
    return { Label: 'Disabled', Tone: 'neutral', Title: 'Provider is disabled.' };

  if (!provider.HealthCheckEnabled || health?.HealthCheckEnabled === false)
    return { Label: 'Monitoring off', Tone: 'neutral', Title: 'Health checks are disabled.' };

  if (!health || !health.LastCheckUtc)
    return { Label: 'Pending', Tone: 'warning', Title: 'Waiting for the first health check.' };

  if (health.IsHealthy)
    return { Label: 'Healthy', Tone: 'success', Title: 'Provider health checks are passing.' };

  return { Label: 'Unhealthy', Tone: 'danger', Title: health.LastError || 'Provider health checks are failing.' };
}

function defaultProviderId(type: string) {
  if (type === 'OpenAI') return 'provider_openai';
  if (type === 'OpenAICompatible') return 'provider_openai_compatible';
  if (type === 'Gemini') return 'provider_gemini';
  return 'provider_ollama_local';
}

function defaultProviderName(type: string) {
  if (type === 'OpenAI') return 'OpenAI';
  if (type === 'OpenAICompatible') return 'OpenAI Compatible';
  if (type === 'Gemini') return 'Gemini';
  return 'Local Ollama';
}

function defaultProviderEndpoint(type: string) {
  if (type === 'OpenAI') return 'https://api.openai.com';
  if (type === 'OpenAICompatible') return 'http://localhost:1234';
  if (type === 'Gemini') return 'https://generativelanguage.googleapis.com';
  return 'http://localhost:11434';
}

function defaultProviderModel(type: string) {
  if (type === 'OpenAI') return 'gpt-4o-mini';
  if (type === 'OpenAICompatible') return 'local-model';
  if (type === 'Gemini') return 'gemini-2.5-flash';
  return 'gemma3:4b';
}

function defaultHealthCheckUrl(type: string, endpoint: string) {
  const normalized = endpoint.trim().replace(/\/+$/, '');
  if (!normalized) return '';

  if (type === 'Ollama') {
    if (normalized.endsWith('/api/tags')) return normalized;
    if (normalized.endsWith('/api')) return `${normalized}/tags`;
    return `${normalized}/api/tags`;
  }

  if (type === 'Gemini') {
    if (normalized.endsWith('/models')) return normalized;
    if (normalized.endsWith('/v1beta')) return `${normalized}/models`;
    return `${normalized}/v1beta/models`;
  }

  if (normalized.endsWith('/models')) return normalized;
  if (normalized.endsWith('/v1')) return `${normalized}/models`;
  return `${normalized}/v1/models`;
}

function formatTimestamp(value?: string | null) {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function formatPercent(value?: number | null) {
  if (value == null || Number.isNaN(value)) return '-';
  return `${value.toFixed(value % 1 === 0 ? 0 : 2)}%`;
}

function formatDuration(milliseconds: number) {
  if (!Number.isFinite(milliseconds) || milliseconds <= 0) return '0s';
  if (milliseconds < 1000) return `${Math.round(milliseconds)}ms`;

  const totalSeconds = Math.floor(milliseconds / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const parts: string[] = [];
  if (days) parts.push(`${days}d`);
  if (hours) parts.push(`${hours}h`);
  if (minutes) parts.push(`${minutes}m`);
  if (seconds || parts.length === 0) parts.push(`${seconds}s`);
  return parts.slice(0, 2).join(' ');
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
