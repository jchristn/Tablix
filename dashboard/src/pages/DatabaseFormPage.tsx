import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type { DatabaseEntry } from '../types';

const emptyEntry: DatabaseEntry = {
  Id: '',
  Name: '',
  Type: 'Sqlite',
  Hostname: '',
  Port: 5432,
  User: '',
  Password: '',
  DatabaseName: '',
  Schema: 'public',
  Filename: '',
  AllowedQueries: ['SELECT'],
  Context: '',
};

export default function DatabaseFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const [entry, setEntry] = useState<DatabaseEntry>({ ...emptyEntry });
  const [allowedStr, setAllowedStr] = useState('SELECT');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (isEdit) loadEntry();
  }, [id]);

  async function loadEntry() {
    const response = await apiFetch(`/v1/database/${id}`);
    if (response.ok) {
      const data = await response.json();
      const dbEntry: DatabaseEntry = {
        Id: data.DatabaseId || id || '',
        Name: data.Name || '',
        Type: data.Type || 'Sqlite',
        Hostname: data.Hostname || '',
        Port: data.Port || 5432,
        User: data.User || '',
        Password: data.Password || '',
        DatabaseName: data.DatabaseName || '',
        Schema: data.Schema || '',
        Filename: data.Filename || '',
        AllowedQueries: data.AllowedQueries || ['SELECT'],
        Context: data.Context || '',
      };
      setEntry(dbEntry);
      setAllowedStr((dbEntry.AllowedQueries || []).join(', '));
    }
  }

  function handleChange(field: keyof DatabaseEntry, value: string | number) {
    setEntry({ ...entry, [field]: value });
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError('');

    const body = {
      ...entry,
      AllowedQueries: allowedStr.split(',').map(s => s.trim()).filter(Boolean),
    };

    try {
      const response = isEdit
        ? await apiFetch(`/v1/database/${id}`, { method: 'PUT', body: JSON.stringify(body) })
        : await apiFetch('/v1/database', { method: 'POST', body: JSON.stringify(body) });

      if (response.ok) {
        navigate(isEdit ? `/databases/${id}` : '/');
      } else {
        const err = await response.json();
        setError(err.Description || err.Message || 'Failed to save.');
      }
    } catch {
      setError('Could not connect to server.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h2 title={isEdit ? 'Modify database connection settings' : 'Configure a new database connection'}>{isEdit ? 'Edit Database' : 'Add Database'}</h2>
      </div>

      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label title="Unique identifier for this database entry (e.g. db_myapp)">ID</label>
            <input title="Must be unique across all database entries" value={entry.Id} onChange={e => handleChange('Id', e.target.value)} disabled={isEdit} placeholder="db_my_database" />
          </div>

          <div className="form-group">
            <label title="Human-readable display name for this database">Name</label>
            <input title="A friendly name to identify this database (e.g. Staging Orders DB)" value={entry.Name || ''} onChange={e => handleChange('Name', e.target.value)} placeholder="My Database" />
          </div>

          <div className="form-group">
            <label title="Database engine type">Type</label>
            <select title="Select the database engine" value={entry.Type} onChange={e => handleChange('Type', e.target.value)}>
              <option value="Sqlite">SQLite</option>
              <option value="Postgresql">PostgreSQL</option>
              <option value="Mysql">MySQL</option>
              <option value="SqlServer">SQL Server</option>
            </select>
          </div>

          {entry.Type === 'Sqlite' ? (
            <div className="form-group">
              <label title="Path to the SQLite database file">Filename</label>
              <input title="Relative or absolute path to the .db file" value={entry.Filename || ''} onChange={e => handleChange('Filename', e.target.value)} placeholder="./database.db" />
            </div>
          ) : (
            <>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 120px', gap: '12px' }}>
                <div className="form-group">
                  <label title="Database server hostname or IP address">Hostname</label>
                  <input title="The host where the database server is running" value={entry.Hostname || ''} onChange={e => handleChange('Hostname', e.target.value)} placeholder="localhost" />
                </div>
                <div className="form-group">
                  <label title="Database server port number (1-65535)">Port</label>
                  <input title="TCP port the database listens on" type="number" value={entry.Port} onChange={e => handleChange('Port', parseInt(e.target.value) || 0)} />
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="form-group">
                  <label title="Database authentication username">User</label>
                  <input title="Username for database authentication" value={entry.User || ''} onChange={e => handleChange('User', e.target.value)} />
                </div>
                <div className="form-group">
                  <label title="Database authentication password">Password</label>
                  <input title="Password for database authentication (stored in cleartext)" type="password" value={entry.Password || ''} onChange={e => handleChange('Password', e.target.value)} />
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="form-group">
                  <label title="Name of the database to connect to">Database Name</label>
                  <input title="The specific database on the server" value={entry.DatabaseName || ''} onChange={e => handleChange('DatabaseName', e.target.value)} />
                </div>
                <div className="form-group">
                  <label title="Database schema to crawl (default: public)">Schema</label>
                  <input title="Schema name used when discovering tables" value={entry.Schema || ''} onChange={e => handleChange('Schema', e.target.value)} placeholder="public" />
                </div>
              </div>
            </>
          )}

          <div className="form-group">
            <label title="SQL statement types permitted for this database (e.g. SELECT, INSERT, UPDATE, DELETE)">Allowed Queries (comma-separated)</label>
            <input title="Only these statement types will be accepted when executing queries" value={allowedStr} onChange={e => setAllowedStr(e.target.value)} placeholder="SELECT, INSERT, UPDATE, DELETE" />
          </div>

          <div className="form-group">
            <label title="Free-form description of the database for AI agents — describe tables, relationships, and typical queries">Context</label>
            <textarea title="This text is provided to AI agents to help them understand the database" rows={4} value={entry.Context || ''} onChange={e => handleChange('Context', e.target.value)} placeholder="Describe the database, its tables, and how they relate..." style={{ fontFamily: 'var(--font-mono)', fontSize: '13px' }} />
          </div>

          {error && <p className="error-text" style={{ marginBottom: '12px' }}>{error}</p>}

          <div style={{ display: 'flex', gap: '8px' }}>
            <button type="submit" className="btn-primary" title={isEdit ? 'Save changes to this database entry' : 'Create a new database entry'} disabled={saving}>
              {saving ? 'Saving...' : (isEdit ? 'Update' : 'Create')}
            </button>
            <button type="button" className="btn-secondary" title="Discard changes and go back" onClick={() => navigate(isEdit ? `/databases/${id}` : '/')}>Cancel</button>
          </div>
        </form>
      </div>
    </div>
  );
}
