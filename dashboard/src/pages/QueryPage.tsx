import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type { DatabaseEntry, EnumerationResult, QueryResult } from '../types';

export default function QueryPage() {
  const navigate = useNavigate();
  const [databases, setDatabases] = useState<DatabaseEntry[]>([]);
  const [selectedDb, setSelectedDb] = useState('');
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<QueryResult | null>(null);
  const [error, setError] = useState('');
  const [executing, setExecuting] = useState(false);

  useEffect(() => { loadDatabases(); }, []);

  async function loadDatabases() {
    try {
      const response = await apiFetch('/v1/database?maxResults=1000');
      if (response.status === 401) { navigate('/login'); return; }
      if (response.ok) {
        const data: EnumerationResult<DatabaseEntry> = await response.json();
        setDatabases(data.Objects);
        if (data.Objects.length > 0) setSelectedDb(data.Objects[0].Id);
      }
    } catch {
      setError('Could not connect to server.');
    }
  }

  async function handleExecute(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedDb || !query.trim()) return;

    setExecuting(true);
    setError('');
    setResult(null);

    try {
      const response = await apiFetch(`/v1/database/${selectedDb}/query`, {
        method: 'POST',
        body: JSON.stringify({ Query: query.replace(/;\s*$/, '') }),
      });

      const data = await response.json();
      if (response.ok && data.Success) {
        setResult(data);
      } else {
        const parts = [data.Error, data.Description].filter(Boolean);
        setError(parts.length > 0 ? parts.join(': ') : (data.Message || 'Query failed.'));
      }
    } catch {
      setError('Could not connect to server.');
    } finally {
      setExecuting(false);
    }
  }

  const columns = result?.Data?.Columns || [];
  const rows = result?.Data?.Rows || [];

  return (
    <div>
      <div className="page-header">
        <h2 title="Execute SQL queries against configured databases">Query</h2>
      </div>

      <div className="card" style={{ marginBottom: '16px' }}>
        <form onSubmit={handleExecute}>
          <div className="form-group">
            <label title="Select which database to run the query against">Database</label>
            <select title="Choose a configured database" value={selectedDb} onChange={e => setSelectedDb(e.target.value)}>
              {databases.map(db => (
                <option key={db.Id} value={db.Id}>{db.Id} — {db.DatabaseName || db.Filename || db.Type}</option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label title="Enter a single SQL statement to execute">SQL Query</label>
            <textarea
              rows={6}
              value={query}
              onChange={e => setQuery(e.target.value)}
              title="Single SQL statement — trailing semicolons are automatically removed"
              placeholder="SELECT * FROM users LIMIT 10"
            />
          </div>

          <button type="submit" className="btn-primary" title="Execute the query against the selected database" disabled={executing || !selectedDb}>
            {executing ? 'Executing...' : 'Execute'}
          </button>
        </form>
      </div>

      {error && <div className="card"><p className="error-text">{error}</p></div>}

      {result && (
        <div className="card">
          <div style={{ marginBottom: '12px', display: 'flex', gap: '16px' }}>
            <span className="muted-text" title="Number of rows returned by the query">{result.RowsReturned} row(s)</span>
            <span className="muted-text" title="Query execution time in milliseconds">{result.TotalMs.toFixed(1)} ms</span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ borderCollapse: 'collapse', border: '1px solid var(--border-color)' }}>
              <thead>
                <tr>
                  {columns.map(col => (
                    <th key={col.Name} title={col.Name + ' (' + col.Type + ')'} style={{ border: '1px solid var(--border-color)' }}>{col.Name}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row, i) => (
                  <tr key={i}>
                    {columns.map(col => {
                      const raw = row[col.Name];
                      const isNull = raw === null || raw === undefined;
                      const text = isNull ? null : String(raw);
                      return (
                        <td key={col.Name} title={isNull ? 'NULL' : text!} style={{
                          fontFamily: 'var(--font-mono)', fontSize: '13px',
                          border: '1px solid var(--border-color)',
                          maxWidth: '280px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap'
                        }}>
                          {isNull ? <span className="muted-text">NULL</span> : text}
                        </td>
                      );
                    })}
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr><td colSpan={columns.length || 1} className="muted-text" style={{ textAlign: 'center' }}>No rows returned.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
