import { useEffect, useState } from 'react';
import { apiFetch } from '../api/client';
import type { DatabaseEntry } from '../types';

interface DatabaseFormModalProps {
  Open: boolean;
  DatabaseId?: string | null;
  OnClose: () => void;
  OnSaved: (id: string) => void;
}

const emptyEntry: DatabaseEntry = {
  Id: '',
  Name: '',
  Type: 'Sqlite',
  Hostname: '',
  Port: null,
  User: '',
  Password: '',
  DatabaseName: '',
  Schema: 'public',
  Filename: '',
  AllowedQueries: ['SELECT'],
  Context: '',
};

const typeDefaults: Record<string, { Port: number; User: string; Schema: string }> = {
  Postgresql: { Port: 5432, User: 'postgres', Schema: 'public' },
  Mysql: { Port: 3306, User: 'root', Schema: 'public' },
  SqlServer: { Port: 1433, User: 'sa', Schema: 'dbo' },
};

const typeLabels: { value: string; label: string }[] = [
  { value: 'Sqlite', label: 'SQLite' },
  { value: 'Postgresql', label: 'PostgreSQL' },
  { value: 'Mysql', label: 'MySQL' },
  { value: 'SqlServer', label: 'SQL Server' },
];

const queryOptions = ['SELECT', 'INSERT', 'UPDATE', 'DELETE', 'WITH', 'CALL', 'CREATE', 'ALTER', 'DROP', 'TRUNCATE', 'MERGE', 'REPLACE'];

export default function DatabaseFormModal({ Open, DatabaseId, OnClose, OnSaved }: DatabaseFormModalProps) {
  const isEdit = !!DatabaseId;
  const [entry, setEntry] = useState<DatabaseEntry>({ ...emptyEntry });
  const [allowed, setAllowed] = useState<string[]>(['SELECT']);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => { if (Open) initializeForm(); }, [Open, DatabaseId]);

  async function initializeForm() {
    setError('');
    setShowPassword(false);

    if (isEdit) {
      await loadEntry(DatabaseId as string);
    } else {
      setEntry({ ...emptyEntry });
      setAllowed(['SELECT']);
    }
  }

  async function loadEntry(id: string) {
    setLoading(true);
    try {
      const response = await apiFetch(`/v1/database/${id}`);
      if (response.status === 404) { setError('Database not found.'); return; }
      if (!response.ok) { setError('Failed to load database.'); return; }

      const data = await response.json();
      const dbEntry: DatabaseEntry = {
        Id: data.DatabaseId || id,
        Name: data.Name || '',
        Type: data.Type || 'Sqlite',
        Hostname: data.Hostname || '',
        Port: data.Port ?? null,
        User: data.User || '',
        Password: data.Password || '',
        HasUser: data.HasUser || false,
        HasPassword: data.HasPassword || false,
        DatabaseName: data.DatabaseName || '',
        Schema: data.Schema || '',
        Filename: data.Filename || '',
        AllowedQueries: data.AllowedQueries || ['SELECT'],
        Context: data.Context || '',
      };
      setEntry(dbEntry);
      setAllowed((dbEntry.AllowedQueries || []).map(query => query.trim().toUpperCase()).filter(Boolean));
    } catch {
      setError('Could not connect to server.');
    } finally {
      setLoading(false);
    }
  }

  function handleChange(field: keyof DatabaseEntry, value: string | number) {
    const updated = { ...entry, [field]: value };

    // Auto-populate sensible defaults when picking a type on a new database.
    if (field === 'Type' && !isEdit && typeof value === 'string' && value in typeDefaults) {
      const defaults = typeDefaults[value];
      if (!entry.Port) updated.Port = defaults.Port;
      if (!entry.User) updated.User = defaults.User;
      if (!entry.Schema) updated.Schema = defaults.Schema;
    }

    setEntry(updated);
  }

  function toggleAllowed(query: string) {
    setAllowed(previous => previous.includes(query)
      ? previous.filter(item => item !== query)
      : [...previous, query]);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError('');

    const body = {
      ...entry,
      AllowedQueries: allowed,
    };

    try {
      const response = isEdit
        ? await apiFetch(`/v1/database/${DatabaseId}`, { method: 'PUT', body: JSON.stringify(body) })
        : await apiFetch('/v1/database', { method: 'POST', body: JSON.stringify(body) });

      if (response.ok) {
        const saved = await response.json().catch(() => null);
        OnSaved(saved?.Id || saved?.DatabaseId || DatabaseId || entry.Id);
      } else {
        const err = await response.json().catch(() => null);
        setError(err?.Description || err?.Message || 'Failed to save.');
      }
    } catch {
      setError('Could not connect to server.');
    } finally {
      setSaving(false);
    }
  }

  if (!Open) return null;

  const isSqlite = entry.Type === 'Sqlite';
  const isMysql = entry.Type === 'Mysql';

  return (
    <div className="modal-backdrop" role="presentation" onClick={() => !saving && OnClose()}>
      <div
        className="modal-panel database-form-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="database-form-title"
        onClick={event => event.stopPropagation()}
      >
        <div className="modal-header">
          <div>
            <h3 id="database-form-title">{isEdit ? 'Edit Database' : 'Add Database'}</h3>
            <p className="muted-text">{isEdit ? (DatabaseId as string) : 'Configure a new database connection'}</p>
          </div>
          <button type="button" className="icon-action" aria-label="Close" title="Close" onClick={OnClose} disabled={saving}>
            <CloseIcon />
          </button>
        </div>

        {loading ? (
          <p className="muted-text" style={{ padding: '24px 0' }}>Loading...</p>
        ) : (
          <form onSubmit={handleSubmit} autoComplete="off" className="database-form">
            <section className="form-section">
              <div className="form-grid form-grid-identity">
                <div className="form-group">
                  <label title="Unique identifier for this database entry (e.g. db_myapp)">ID</label>
                  <input
                    title="Must be unique across all database entries"
                    value={entry.Id}
                    onChange={e => handleChange('Id', e.target.value)}
                    disabled={isEdit}
                    placeholder="db_my_database"
                    autoComplete="off"
                  />
                </div>
                <div className="form-group">
                  <label title="Human-readable display name for this database">Name</label>
                  <input
                    title="A friendly name to identify this database"
                    value={entry.Name || ''}
                    onChange={e => handleChange('Name', e.target.value)}
                    placeholder="My Database"
                    autoComplete="off"
                  />
                </div>
                <div className="form-group">
                  <label title="Database engine type">Type</label>
                  <select title="Select the database engine" value={entry.Type} onChange={e => handleChange('Type', e.target.value)}>
                    {typeLabels.map(option => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </div>
              </div>
            </section>

            <section className="form-section">
              <h4 className="form-section-title">Connection</h4>
              {isSqlite ? (
                <div className="form-group" style={{ marginBottom: 0 }}>
                  <label title="Path to the SQLite database file">Filename</label>
                  <input
                    title="Relative or absolute path to the .db file"
                    value={entry.Filename || ''}
                    onChange={e => handleChange('Filename', e.target.value)}
                    placeholder="./database.db"
                    autoComplete="off"
                  />
                </div>
              ) : (
                <>
                  <div className="form-grid form-grid-host">
                    <div className="form-group">
                      <label title="Database server hostname or IP address">Hostname</label>
                      <input
                        title="The host where the database server is running"
                        value={entry.Hostname || ''}
                        onChange={e => handleChange('Hostname', e.target.value)}
                        placeholder="localhost"
                        autoComplete="off"
                      />
                    </div>
                    <div className="form-group">
                      <label title="Database server port number (1-65535)">Port</label>
                      <input
                        title="TCP port the database listens on"
                        type="number"
                        value={entry.Port ?? ''}
                        onChange={e => { const v = parseInt(e.target.value); handleChange('Port', isNaN(v) ? '' as unknown as number : v); }}
                        autoComplete="off"
                      />
                    </div>
                  </div>

                  <div className="form-grid" style={{ gridTemplateColumns: '1fr 1fr 1fr', marginBottom: 0 }}>
                    <div className="form-group" style={{ marginBottom: 0 }}>
                      <label title="Database authentication username">User</label>
                      <input
                        title="Username for database authentication."
                        value={entry.User || ''}
                        onChange={e => handleChange('User', e.target.value)}
                        autoComplete="off"
                        name="tablix-db-user"
                      />
                    </div>
                    <div className="form-group" style={{ marginBottom: 0 }}>
                      <label title="Database authentication password">Password</label>
                      <div className="input-with-affix">
                        <input
                          title="Password for database authentication."
                          type={showPassword ? 'text' : 'password'}
                          value={entry.Password || ''}
                          onChange={e => handleChange('Password', e.target.value)}
                          autoComplete="new-password"
                          name="tablix-db-password"
                        />
                        <button
                          type="button"
                          className="input-affix-button"
                          onClick={() => setShowPassword(previous => !previous)}
                          title={showPassword ? 'Hide password' : 'Show password'}
                          aria-label={showPassword ? 'Hide password' : 'Show password'}
                          aria-pressed={showPassword}
                          tabIndex={-1}
                        >
                          {showPassword ? <EyeOffIcon /> : <EyeIcon />}
                        </button>
                      </div>
                    </div>
                    <div className="form-group" style={{ marginBottom: 0 }}>
                      <label title={isMysql ? 'Name of the database/schema to connect to' : 'Name of the database to connect to'}>Database Name</label>
                      <input
                        title="The specific database on the server"
                        value={entry.DatabaseName || ''}
                        onChange={e => handleChange('DatabaseName', e.target.value)}
                        placeholder={isMysql ? 'schema to crawl' : ''}
                        autoComplete="off"
                      />
                    </div>
                  </div>

                  {!isMysql && (
                    <div className="form-group" style={{ marginBottom: 0 }}>
                      <label title="Database schema to crawl (default: public)">Schema</label>
                      <input
                        title="Schema name used when discovering tables"
                        value={entry.Schema || ''}
                        onChange={e => handleChange('Schema', e.target.value)}
                        placeholder="public"
                        autoComplete="off"
                      />
                    </div>
                  )}
                </>
              )}
            </section>

            <section className="form-section">
              <h4 className="form-section-title">Permissions &amp; Context</h4>
              <div className="form-group">
                <label title="SQL statement types permitted for this database">Allowed Queries</label>
                <div className="checkbox-grid" role="group" aria-label="Allowed query types">
                  {[...queryOptions, ...allowed.filter(query => !queryOptions.includes(query))].map(query => (
                    <label key={query} className="checkbox-option" title={`Permit ${query} statements`}>
                      <input
                        type="checkbox"
                        checked={allowed.includes(query)}
                        onChange={() => toggleAllowed(query)}
                      />
                      <span>{query}</span>
                    </label>
                  ))}
                </div>
              </div>
              <div className="form-group" style={{ marginBottom: 0 }}>
                <label title="Free-form description of the database for AI agents">Context</label>
                <textarea
                  title="This text is provided to AI agents to help them understand the database"
                  rows={12}
                  value={entry.Context || ''}
                  onChange={e => handleChange('Context', e.target.value)}
                  placeholder="Describe the database, its tables, and how they relate..."
                  style={{ fontFamily: 'var(--font-mono)', fontSize: '13px' }}
                />
              </div>
            </section>

            {error && <p className="error-text" style={{ margin: '4px 0 0' }}>{error}</p>}

            <div className="modal-actions" style={{ borderTop: '1px solid var(--border-color)', paddingTop: '16px' }}>
              <button type="button" className="btn-secondary" onClick={OnClose} disabled={saving}>Cancel</button>
              <button type="submit" className="btn-primary" disabled={saving}>
                {saving ? 'Saving...' : (isEdit ? 'Save Changes' : 'Create')}
              </button>
            </div>
          </form>
        )}
      </div>
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

function EyeIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}

function EyeOffIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c6.5 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68" />
      <path d="M6.61 6.61A13.53 13.53 0 0 0 2 12s3.5 7 10 7a9.74 9.74 0 0 0 5.39-1.61" />
      <path d="M14.12 14.12a3 3 0 1 1-4.24-4.24" />
      <path d="m2 2 20 20" />
    </svg>
  );
}
