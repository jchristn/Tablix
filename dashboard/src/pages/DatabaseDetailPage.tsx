import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import type { DatabaseDetail } from '../types';

export default function DatabaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<DatabaseDetail | null>(null);
  const [error, setError] = useState('');
  const [crawling, setCrawling] = useState(false);

  useEffect(() => { loadDetail(); }, [id]);

  async function loadDetail() {
    try {
      const response = await apiFetch(`/v1/database/${id}`);
      if (response.status === 401) { navigate('/login'); return; }
      if (response.status === 404) { setError('Database not found.'); return; }
      const data: DatabaseDetail = await response.json();
      setDetail(data);
      setError('');
    } catch {
      setError('Could not connect to server.');
    }
  }

  async function handleCrawl() {
    setCrawling(true);
    try {
      const response = await apiFetch(`/v1/database/${id}/crawl`, { method: 'POST' });
      if (response.ok) {
        const data: DatabaseDetail = await response.json();
        setDetail(data);
      }
    } finally {
      setCrawling(false);
    }
  }

  async function handleDelete() {
    if (!confirm('Delete this database entry?')) return;
    await apiFetch(`/v1/database/${id}`, { method: 'DELETE' });
    navigate('/');
  }

  if (error) return <p className="error-text">{error}</p>;
  if (!detail) return <p className="muted-text">Loading...</p>;

  return (
    <div>
      <div className="page-header">
        <h2 title="Database entry details and schema geometry">{detail.DatabaseName || detail.DatabaseId}</h2>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button className="btn-primary" title="Re-discover tables, columns, foreign keys, and indexes" onClick={handleCrawl} disabled={crawling}>
            {crawling ? 'Crawling...' : 'Re-Crawl'}
          </button>
          <button className="btn-secondary" title="Edit this database entry's connection settings and context" onClick={() => navigate(`/databases/${id}/edit`)}>Edit</button>
          <button className="btn-danger" title="Permanently remove this database entry from the configuration" onClick={handleDelete}>Delete</button>
        </div>
      </div>

      <div className="card" style={{ marginBottom: '16px' }}>
        <table>
          <tbody>
            <tr><td title="Unique identifier for this database entry" style={{ fontWeight: 500, width: '140px' }}>ID</td><td>{detail.DatabaseId}</td></tr>
            <tr><td title="Database engine type" style={{ fontWeight: 500 }}>Type</td><td>{detail.Type}</td></tr>
            <tr><td title="Database schema name" style={{ fontWeight: 500 }}>Schema</td><td>{detail.Schema || '—'}</td></tr>
            <tr>
              <td title="Whether the schema has been successfully crawled" style={{ fontWeight: 500 }}>Status</td>
              <td>
                {detail.IsCrawled
                  ? <span className="badge badge-success" title="Schema geometry has been successfully discovered">Crawled</span>
                  : <span className="badge badge-warning" title="Schema crawl has not completed — geometry may be unavailable">Degraded</span>
                }
              </td>
            </tr>
            {detail.CrawledUtc && <tr><td title="Timestamp of the last successful crawl" style={{ fontWeight: 500 }}>Last Crawl</td><td>{new Date(detail.CrawledUtc).toLocaleString()}</td></tr>}
            {detail.CrawlError && <tr><td title="Error from the last crawl attempt" style={{ fontWeight: 500 }}>Error</td><td className="error-text">{detail.CrawlError}</td></tr>}
          </tbody>
        </table>
      </div>

      {detail.Context && (
        <div className="card" style={{ marginBottom: '16px' }}>
          <h3 title="User-supplied description of the database for AI agents" style={{ fontSize: '14px', marginBottom: '8px', color: 'var(--text-secondary)' }}>CONTEXT</h3>
          <p style={{ whiteSpace: 'pre-wrap' }}>{detail.Context}</p>
        </div>
      )}

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
                    FK: {table.ForeignKeys.map(fk => `${fk.ColumnName} → ${fk.ReferencedTable}.${fk.ReferencedColumn}`).join(', ')}
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
