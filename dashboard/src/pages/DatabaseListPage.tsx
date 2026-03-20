import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type { DatabaseEntry, EnumerationResult } from '../types';

export default function DatabaseListPage() {
  const [result, setResult] = useState<EnumerationResult<DatabaseEntry> | null>(null);
  const [error, setError] = useState('');
  const [skip, setSkip] = useState(0);
  const [jumpPage, setJumpPage] = useState('');
  const maxResults = 20;
  const navigate = useNavigate();

  useEffect(() => { loadDatabases(); }, [skip]);

  async function loadDatabases() {
    try {
      const response = await apiFetch(`/v1/database?maxResults=${maxResults}&skip=${skip}`);
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) { setError('Failed to load databases.'); return; }
      const data: EnumerationResult<DatabaseEntry> = await response.json();
      setResult(data);
      setError('');
    } catch {
      setError('Could not connect to server.');
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

  return (
    <div>
      <div className="page-header">
        <h2 title="Configured database connections">Databases</h2>
        <button className="btn-primary" title="Add a new database connection" onClick={() => navigate('/databases/new')}>Add Database</button>
      </div>

      {error && <p className="error-text">{error}</p>}

      {result && (
        <div className="card">
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0 0 12px 0',
            borderBottom: '1px solid var(--border-color)',
            marginBottom: '0',
          }}>
            <span className="muted-text" title="Total number of configured databases">{totalRecords} record{totalRecords !== 1 ? 's' : ''}</span>

            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <button
                className="btn-secondary"
                disabled={currentPage <= 1}
                onClick={() => goToPage(currentPage - 1)}
                title="Go to the previous page"
                style={{ padding: '4px 10px', fontSize: '13px' }}
              >
                Prev
              </button>

              <span className="muted-text" title="Current page position" style={{ fontSize: '13px' }}>
                Page {currentPage} of {totalPages}
              </span>

              <button
                className="btn-secondary"
                disabled={result.EndOfResults}
                onClick={() => goToPage(currentPage + 1)}
                title="Go to the next page"
                style={{ padding: '4px 10px', fontSize: '13px' }}
              >
                Next
              </button>

              <form onSubmit={handleJump} style={{ display: 'flex', alignItems: 'center', gap: '4px', marginLeft: '8px' }}>
                <input
                  type="number"
                  min={1}
                  max={totalPages}
                  value={jumpPage}
                  onChange={e => setJumpPage(e.target.value)}
                  placeholder="#"
                  title="Enter a page number to jump to"
                  style={{ width: '52px', padding: '4px 6px', fontSize: '13px' }}
                />
                <button type="submit" className="btn-secondary" title="Jump to the specified page" style={{ padding: '4px 10px', fontSize: '13px' }}>Go</button>
              </form>

              <button
                className="btn-secondary"
                onClick={loadDatabases}
                title="Reload the database list from the server"
                style={{ padding: '4px 10px', fontSize: '13px', marginLeft: '8px' }}
              >
                Refresh
              </button>
            </div>
          </div>

          <table>
            <thead>
              <tr>
                <th title="Unique database entry identifier">ID</th>
                <th title="Database engine type (Sqlite, Postgresql, Mysql, SqlServer)">Type</th>
                <th title="Database or file name">Name</th>
                <th title="Database schema">Schema</th>
              </tr>
            </thead>
            <tbody>
              {result.Objects.map(db => (
                <tr key={db.Id} title="Click to view database details and schema geometry" style={{ cursor: 'pointer' }} onClick={() => navigate(`/databases/${db.Id}`)}>
                  <td><Link to={`/databases/${db.Id}`} title={"View details for " + db.Id}>{db.Id}</Link></td>
                  <td title={db.Type}>{db.Type}</td>
                  <td title={db.DatabaseName || db.Filename || '—'}>{db.DatabaseName || db.Filename || '—'}</td>
                  <td title={db.Schema || '—'}>{db.Schema || '—'}</td>
                </tr>
              ))}
              {result.Objects.length === 0 && (
                <tr><td colSpan={4} style={{ textAlign: 'center' }} className="muted-text">No databases configured.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
