import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type { ChatOptionsResponse, SettingsReadResponse, SettingsUpdateRequest } from '../types';

export default function SettingsPage() {
  const navigate = useNavigate();
  const [settings, setSettings] = useState<SettingsReadResponse | null>(null);
  const [providers, setProviders] = useState<{ Id: string; Name: string | null }[]>([]);
  const [apiKeysText, setApiKeysText] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => { loadSettings(); loadProviders(); }, []);

  async function loadSettings() {
    try {
      const response = await apiFetch('/v1/settings');
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to load settings.'); return; }

      const data: SettingsReadResponse = await response.json();
      setSettings(data);
      setApiKeysText(data.ApiKeys.join('\n'));
      setError('');
    } catch {
      setError('Could not connect to server.');
    }
  }

  async function loadProviders() {
    try {
      const response = await apiFetch('/v1/chat/options');
      if (!response.ok) return;

      const data: ChatOptionsResponse = await response.json();
      setProviders((data.Providers || []).map(provider => ({ Id: provider.Id, Name: provider.Name })));
    } catch {
      setProviders([]);
    }
  }

  async function handleSave(event: React.FormEvent) {
    event.preventDefault();
    if (!settings) return;

    setSaving(true);
    setError('');
    setMessage('');

    const update: SettingsUpdateRequest = {
      Persistence: settings.Persistence,
      Rest: settings.Rest,
      Logging: settings.Logging,
      ApiKeys: apiKeysText.split('\n').map(key => key.trim()).filter(Boolean),
      Chat: settings.Chat,
    };

    try {
      const response = await apiFetch('/v1/settings', {
        method: 'PUT',
        body: JSON.stringify(update),
      });

      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        const text = await response.text();
        setError(text || 'Failed to save settings.');
        return;
      }

      const data: SettingsReadResponse = await response.json();
      setSettings(data);
      setApiKeysText(data.ApiKeys.join('\n'));
      setMessage('Settings saved.');
    } catch {
      setError('Could not connect to server.');
    } finally {
      setSaving(false);
    }
  }

  function updateSettings(mutator: (draft: SettingsReadResponse) => void) {
    setSettings(previous => {
      if (!previous) return previous;
      const draft: SettingsReadResponse = JSON.parse(JSON.stringify(previous));
      mutator(draft);
      return draft;
    });
  }

  function isRestartPath(path: string) {
    return settings?.RestartRequiredPaths.includes(path) || false;
  }

  if (!settings) {
    return <p className={error ? 'error-text' : 'muted-text'}>{error || 'Loading settings...'}</p>;
  }

  return (
    <div>
      <div className="page-header">
        <h2 title="View and edit server settings">Settings</h2>
        <button className="btn-primary" type="submit" form="settings-form" disabled={saving}>{saving ? 'Saving...' : 'Save Settings'}</button>
      </div>

      <form id="settings-form" className="settings-form" onSubmit={handleSave}>
        <section className="settings-section">
          <h3>Persistence <RestartBadge show={isRestartPath('Persistence.Filename') || isRestartPath('Persistence.Type')} /></h3>
          <div className="settings-grid">
            <Field label="Database Type" restart={isRestartPath('Persistence.Type')}>
              <select value={settings.Persistence.Type} onChange={event => updateSettings(draft => { draft.Persistence.Type = event.target.value; })}>
                <option value="Sqlite">SQLite</option>
              </select>
            </Field>
            <Field label="Database Filename" restart={isRestartPath('Persistence.Filename')}>
              <input value={settings.Persistence.Filename} onChange={event => updateSettings(draft => { draft.Persistence.Filename = event.target.value; })} />
            </Field>
            <Field label="Resolved Filename">
              <input value={settings.PersistenceHealth?.ResolvedFilename || ''} readOnly />
            </Field>
            <Field label="Persistence Health">
              <input value={settings.PersistenceHealth?.CanOpen ? 'Open' : settings.PersistenceHealth?.Error || 'Unavailable'} readOnly />
            </Field>
          </div>
        </section>

        <section className="settings-section">
          <h3>REST and MCP <RestartBadge show={isRestartPath('Rest.Port')} /></h3>
          <div className="settings-grid">
            <Field label="REST Hostname" restart={isRestartPath('Rest.Hostname')}>
              <input value={settings.Rest.Hostname} onChange={event => updateSettings(draft => { draft.Rest.Hostname = event.target.value; })} />
            </Field>
            <Field label="REST Port" restart={isRestartPath('Rest.Port')}>
              <input type="number" value={settings.Rest.Port} onChange={event => updateSettings(draft => { draft.Rest.Port = parseNumber(event.target.value, 9100); })} />
            </Field>
            <Field label="MCP Port" restart={isRestartPath('Rest.McpPort')}>
              <input type="number" value={settings.Rest.McpPort} onChange={event => updateSettings(draft => { draft.Rest.McpPort = parseNumber(event.target.value, 9102); })} />
            </Field>
            <label className="toggle-row settings-toggle" title="Enable TLS for REST listener">
              <input type="checkbox" checked={settings.Rest.Ssl} onChange={event => updateSettings(draft => { draft.Rest.Ssl = event.target.checked; })} />
              <span>SSL enabled</span>
              <RestartBadge show={isRestartPath('Rest.Ssl')} />
            </label>
          </div>
        </section>

        <section className="settings-section">
          <h3>API Keys</h3>
          <textarea rows={4} value={apiKeysText} onChange={event => setApiKeysText(event.target.value)} />
        </section>

        <section className="settings-section">
          <h3>Logging <RestartBadge show={isRestartPath('Logging.FileLogging')} /></h3>
          <div className="settings-grid">
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Logging.ConsoleLogging} onChange={event => updateSettings(draft => { draft.Logging.ConsoleLogging = event.target.checked; })} />
              <span>Console logging</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Logging.FileLogging} onChange={event => updateSettings(draft => { draft.Logging.FileLogging = event.target.checked; })} />
              <span>File logging</span>
            </label>
            <Field label="Log Directory" restart={isRestartPath('Logging.LogDirectory')}>
              <input value={settings.Logging.LogDirectory} onChange={event => updateSettings(draft => { draft.Logging.LogDirectory = event.target.value; })} />
            </Field>
            <Field label="Log Filename" restart={isRestartPath('Logging.LogFilename')}>
              <input value={settings.Logging.LogFilename} onChange={event => updateSettings(draft => { draft.Logging.LogFilename = event.target.value; })} />
            </Field>
            <Field label="Minimum Severity" restart={isRestartPath('Logging.MinimumSeverity')}>
              <input type="number" value={settings.Logging.MinimumSeverity} onChange={event => updateSettings(draft => { draft.Logging.MinimumSeverity = parseNumber(event.target.value, 0); })} />
            </Field>
          </div>
        </section>

        <section className="settings-section">
          <h3>Chat</h3>
          <div className="settings-grid">
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.Enabled} onChange={event => updateSettings(draft => { draft.Chat.Enabled = event.target.checked; })} />
              <span>Chat enabled</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.DefaultStreaming} onChange={event => updateSettings(draft => { draft.Chat.DefaultStreaming = event.target.checked; })} />
              <span>Default streaming</span>
            </label>
            <Field label="Default Provider">
              <select value={settings.Chat.DefaultProviderId || ''} onChange={event => updateSettings(draft => { draft.Chat.DefaultProviderId = event.target.value || null; })}>
                <option value="">No default provider</option>
                {providers.map(provider => <option key={provider.Id} value={provider.Id}>{provider.Name || provider.Id}</option>)}
              </select>
            </Field>
            <Field label="Max Context Tables">
              <input type="number" value={settings.Chat.MaxContextTables} onChange={event => updateSettings(draft => { draft.Chat.MaxContextTables = parseNumber(event.target.value, 100); })} />
            </Field>
          </div>
          <div className="form-group" style={{ marginTop: '12px' }}>
            <label>System Prompt</label>
            <textarea rows={4} value={settings.Chat.SystemPrompt || ''} onChange={event => updateSettings(draft => { draft.Chat.SystemPrompt = event.target.value; })} />
          </div>
        </section>

        <section className="settings-section">
          <h3>Prompt Processing</h3>
          <div className="settings-grid">
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.PromptProcessing.Enabled} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.Enabled = event.target.checked; })} />
              <span>Enabled</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.PromptProcessing.PreferNativeToolCalls} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.PreferNativeToolCalls = event.target.checked; })} />
              <span>Prefer native tools</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.PromptProcessing.RequireExecutionForDataRequests} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.RequireExecutionForDataRequests = event.target.checked; })} />
              <span>Execute data requests</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.PromptProcessing.AllowSqlOnlyByExplicitRequest} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.AllowSqlOnlyByExplicitRequest = event.target.checked; })} />
              <span>Honor SQL-only requests</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.PromptProcessing.FallbackWhenNativeToolNotCalled} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.FallbackWhenNativeToolNotCalled = event.target.checked; })} />
              <span>Server fallback</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.PromptProcessing.RetryAfterSchemaRefresh} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.RetryAfterSchemaRefresh = event.target.checked; })} />
              <span>Retry after schema refresh</span>
            </label>
            <Field label="Max Native Tool Iterations">
              <input type="number" value={settings.Chat.PromptProcessing.MaxNativeToolIterations} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.MaxNativeToolIterations = parseNumber(event.target.value, 4); })} />
            </Field>
            <Field label="Max Planning Attempts">
              <input type="number" value={settings.Chat.PromptProcessing.MaxPlanningAttempts} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.MaxPlanningAttempts = parseNumber(event.target.value, 2); })} />
            </Field>
            <Field label="Planner Temperature">
              <input type="number" step="0.1" value={settings.Chat.PromptProcessing.PlannerTemperature} onChange={event => updateSettings(draft => { draft.Chat.PromptProcessing.PlannerTemperature = Number(event.target.value); })} />
            </Field>
          </div>
        </section>

        <section className="settings-section">
          <h3>Chat Tools</h3>
          <div className="settings-grid">
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.Tools.Enabled} onChange={event => updateSettings(draft => { draft.Chat.Tools.Enabled = event.target.checked; })} />
              <span>Tools enabled</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.Tools.AllowReadOnlyQueries} onChange={event => updateSettings(draft => { draft.Chat.Tools.AllowReadOnlyQueries = event.target.checked; })} />
              <span>Read-only queries</span>
            </label>
            <label className="toggle-row settings-toggle">
              <input type="checkbox" checked={settings.Chat.Tools.AllowContextUpdates} onChange={event => updateSettings(draft => { draft.Chat.Tools.AllowContextUpdates = event.target.checked; })} />
              <span>Context updates</span>
            </label>
            <Field label="Max Tool Iterations">
              <input type="number" value={settings.Chat.Tools.MaxToolIterations} onChange={event => updateSettings(draft => { draft.Chat.Tools.MaxToolIterations = parseNumber(event.target.value, 8); })} />
            </Field>
            <Field label="Max Tool Calls">
              <input type="number" value={settings.Chat.Tools.MaxToolCalls} onChange={event => updateSettings(draft => { draft.Chat.Tools.MaxToolCalls = parseNumber(event.target.value, 20); })} />
            </Field>
            <Field label="Tool Timeout Ms">
              <input type="number" value={settings.Chat.Tools.ToolTimeoutMs} onChange={event => updateSettings(draft => { draft.Chat.Tools.ToolTimeoutMs = parseNumber(event.target.value, 30000); })} />
            </Field>
            <Field label="Max Tool Output Characters">
              <input type="number" value={settings.Chat.Tools.MaxToolOutputCharacters} onChange={event => updateSettings(draft => { draft.Chat.Tools.MaxToolOutputCharacters = parseNumber(event.target.value, 12000); })} />
            </Field>
          </div>
        </section>

        {error && <p className="error-text">{error}</p>}
        {message && <p className="muted-text">{message}</p>}
      </form>
    </div>
  );
}

function Field({ label, restart, children }: { label: string; restart?: boolean; children: React.ReactNode }) {
  return (
    <div className="form-group">
      <label>{label} <RestartBadge show={restart || false} /></label>
      {children}
    </div>
  );
}

function RestartBadge({ show }: { show: boolean }) {
  if (!show) return null;
  return <span className="restart-badge" title="Saved immediately; requires server restart to affect the active server process">Restart</span>;
}

function parseNumber(value: string, fallback: number) {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}
