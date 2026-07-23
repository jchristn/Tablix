import { useEffect, useMemo, useState, type MouseEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api/client';
import ButtonBusyContent from '../components/ButtonBusyContent';
import RecordViewModal, { isInteractiveRowClick, type RecordViewRow } from '../components/RecordViewModal';
import type {
  BuildTableContextResponse,
  ChatOptionsResponse,
  DatabaseDetail,
  TableContextRead,
  TableDetail,
} from '../types';

export default function TablesPage() {
  const navigate = useNavigate();
  const [options, setOptions] = useState<ChatOptionsResponse | null>(null);
  const [databaseId, setDatabaseId] = useState('');
  const [providerId, setProviderId] = useState('');
  const [detail, setDetail] = useState<DatabaseDetail | null>(null);
  const [prompt, setPrompt] = useState(defaultTableContextPrompt());
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [buildingAll, setBuildingAll] = useState(false);
  const [buildingTableIds, setBuildingTableIds] = useState<Set<string>>(() => new Set());
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [viewTable, setViewTable] = useState<TableDetail | null>(null);

  useEffect(() => { loadOptions(); }, []);

  useEffect(() => {
    if (databaseId) {
      loadDetail(databaseId);
    } else {
      setDetail(null);
    }
  }, [databaseId]);

  const selectedProvider = useMemo(
    () => options?.Providers.find(provider => provider.Id === providerId) || null,
    [options, providerId]
  );

  const tables = detail?.Tables || [];
  const tableIds = tables.map(table => table.TableId).filter((tableId): tableId is string => Boolean(tableId));
  const busy = buildingAll || buildingTableIds.size > 0;
  const canBuild = Boolean(detail?.IsCrawled && providerId && prompt.trim() && tableIds.length > 0);

  async function loadOptions() {
    setLoadingOptions(true);
    setError('');

    try {
      const response = await apiFetch('/v1/chat/options');
      if (response.status === 401) { navigate('/login'); return; }
      if (!response.ok) {
        setError('Failed to load dashboard options.');
        return;
      }

      const data: ChatOptionsResponse = await response.json();
      setOptions(data);
      setDatabaseId(previous => previous || data.Databases[0]?.Id || '');
      setProviderId(previous => previous || selectAvailableProviderId(data));
    } catch {
      setError('Could not connect to server.');
    } finally {
      setLoadingOptions(false);
    }
  }

  async function loadDetail(id: string) {
    setLoadingDetail(true);
    setError('');
    setMessage('');

    try {
      const response = await apiFetch(`/v1/database/${id}`);
      if (response.status === 401) { navigate('/login'); return; }
      if (response.status === 404) {
        setError('Database not found.');
        return;
      }
      if (!response.ok) {
        setError('Failed to load database tables.');
        return;
      }

      const data: DatabaseDetail = await response.json();
      setDetail(data);
      setViewTable(current => current ? data.Tables.find(table => table.TableId === current.TableId) || current : current);
    } catch {
      setError('Could not connect to server.');
    } finally {
      setLoadingDetail(false);
    }
  }

  async function buildOneTableContext(tableId: string) {
    if (!detail) throw new Error('No database selected.');

    const response = await apiFetch(`/v1/database/${detail.DatabaseId}/table-context/${tableId}/build`, {
      method: 'POST',
      body: JSON.stringify({
        ProviderId: providerId || null,
        Prompt: prompt,
        TableIds: [tableId],
      }),
    });

    if (response.status === 401) {
      navigate('/login');
      throw new Error('Unauthorized.');
    }

    const result: BuildTableContextResponse = await readJsonResponse(response, 'Table context generation failed.');
    if (!response.ok || !result.Success) {
      throw new Error(result.Error || 'Table context generation failed.');
    }

    updateTableContexts(result.Objects || []);
    return result;
  }

  async function buildTableContext(table: TableDetail) {
    if (!table.TableId || !canBuild) return;

    setError('');
    setMessage('');
    markTableBuilding(table.TableId, true);
    try {
      await buildOneTableContext(table.TableId);
      setMessage(`Context built for ${table.SchemaName}.${table.TableName}.`);
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Table context generation failed.');
    } finally {
      markTableBuilding(table.TableId, false);
    }
  }

  async function buildAllContexts() {
    if (!canBuild || !detail) return;

    setBuildingAll(true);
    setError('');
    setMessage('');

    const failures: string[] = [];
    let completed = 0;
    let nextIndex = 0;
    const concurrency = clampConcurrency(selectedProvider?.MaxConcurrentRequests);
    const tableNameById = new Map(tables.map(table => [table.TableId, `${table.SchemaName}.${table.TableName}`]));

    async function worker() {
      while (nextIndex < tableIds.length) {
        const tableId = tableIds[nextIndex];
        nextIndex++;
        markTableBuilding(tableId, true);

        try {
          await buildOneTableContext(tableId);
          completed++;
        } catch (ex) {
          const label = tableNameById.get(tableId) || tableId;
          const suffix = ex instanceof Error ? `: ${ex.message}` : '';
          failures.push(`${label}${suffix}`);
        } finally {
          markTableBuilding(tableId, false);
        }
      }
    }

    try {
      const workerCount = Math.min(concurrency, Math.max(tableIds.length, 1));
      await Promise.all(Array.from({ length: workerCount }, () => worker()));

      if (failures.length > 0) {
        setError(`Built context for ${completed} table(s); failed for ${failures.join(', ')}.`);
      } else {
        setMessage(`Built context for ${completed} table(s).`);
      }
    } finally {
      setBuildingAll(false);
    }
  }

  function updateTableContexts(contexts: TableContextRead[]) {
    const contextByTableId = new Map<string, string | null>();
    contexts.forEach(context => {
      if (context.TableId) contextByTableId.set(context.TableId, context.Context);
    });

    setDetail(previous => {
      if (!previous) return previous;
      return {
        ...previous,
        Tables: previous.Tables.map(table => (
          table.TableId && contextByTableId.has(table.TableId)
            ? { ...table, Context: contextByTableId.get(table.TableId) || null }
            : table
        )),
      };
    });

    setViewTable(previous => (
      previous?.TableId && contextByTableId.has(previous.TableId)
        ? { ...previous, Context: contextByTableId.get(previous.TableId) || null }
        : previous
    ));
  }

  function markTableBuilding(tableId: string, building: boolean) {
    setBuildingTableIds(previous => {
      const next = new Set(previous);
      if (building) {
        next.add(tableId);
      } else {
        next.delete(tableId);
      }
      return next;
    });
  }

  function openTableRecord(table: TableDetail, event: MouseEvent<HTMLTableRowElement>) {
    if (isInteractiveRowClick(event)) return;
    setViewTable(table);
  }

  function tableRows(table: TableDetail): RecordViewRow[] {
    return [
      { Label: 'Schema', Value: table.SchemaName },
      { Label: 'Table', Value: table.TableName },
      { Label: 'Table ID', Value: table.TableId },
      { Label: 'Columns', Value: table.Columns.length },
      { Label: 'Relationships', Value: table.ForeignKeys.length },
      { Label: 'Indexes', Value: table.Indexes.length },
      { Label: 'Context', Value: table.Context },
    ];
  }

  if (loadingOptions) return <p className="muted-text">Loading...</p>;

  return (
    <div>
      <div className="page-header">
        <div>
          <h2 title="Browse crawled tables and generate durable table context">Tables</h2>
          <p className="muted-text">Select a database, inspect its crawled tables, and build table context.</p>
        </div>
        <button className="btn-primary" onClick={buildAllContexts} disabled={!canBuild || busy}>
          {buildingAll ? 'Building...' : 'Build Contexts'}
        </button>
      </div>

      <div className="card tables-control-panel" style={{ marginBottom: '16px' }}>
        <div className="settings-grid">
          <div className="form-group">
            <label title="Database whose crawled tables should be displayed">Database</label>
            <select value={databaseId} onChange={event => setDatabaseId(event.target.value)} disabled={busy || (options?.Databases.length || 0) === 0}>
              {(options?.Databases || []).map(database => (
                <option key={database.Id} value={database.Id}>
                  {database.Name || database.DatabaseName || database.Filename || database.Id}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label title="Model provider used for table context generation">Provider</label>
            <select value={providerId} onChange={event => setProviderId(event.target.value)} disabled={busy || (options?.Providers.length || 0) === 0}>
              {(options?.Providers || []).map(provider => (
                <option key={provider.Id} value={provider.Id}>{provider.Name || provider.Id} - {provider.Model || provider.Type}</option>
              ))}
            </select>
          </div>
        </div>

        <div className="form-group">
          <label title="Instructions sent to the model for each table">Generation Instructions</label>
          <textarea value={prompt} onChange={event => setPrompt(event.target.value)} rows={4} disabled={busy} />
        </div>

        {detail && !detail.IsCrawled && (
          <p className="error-text">Crawl this database before building table context.</p>
        )}
        {options && options.Databases.length === 0 && (
          <p className="muted-text">No databases configured.</p>
        )}
        {options && options.Providers.length === 0 && (
          <p className="error-text">No model providers configured.</p>
        )}
        {error && <p className="error-text">{error}</p>}
        {message && <p className="muted-text">{message}</p>}
      </div>

      <div className="card">
        <div className="table-list-header">
          <h3 title="Tables discovered during the latest successful crawl">Tables ({tables.length})</h3>
          <button className="btn-secondary compact-button" onClick={() => databaseId && loadDetail(databaseId)} disabled={busy || loadingDetail}>
            {loadingDetail ? 'Loading...' : 'Refresh'}
          </button>
        </div>

        {loadingDetail ? (
          <p className="muted-text">Loading...</p>
        ) : tables.length === 0 ? (
          <p className="muted-text">No tables found for the selected database.</p>
        ) : (
          <div className="wide-table">
            <table className="tables-page-table">
              <thead>
                <tr>
                  <th>Schema</th>
                  <th>Table</th>
                  <th>Columns</th>
                  <th>Relationships</th>
                  <th>Indexes</th>
                  <th>Context</th>
                  <th className="actions-column">Actions</th>
                </tr>
              </thead>
              <tbody>
                {tables.map(table => {
                  const rowKey = table.TableId || `${table.SchemaName}.${table.TableName}`;
                  const rowBuilding = Boolean(table.TableId && buildingTableIds.has(table.TableId));
                  return (
                    <tr key={rowKey} title="Click to view table details" style={{ cursor: 'pointer' }} onClick={event => openTableRecord(table, event)}>
                      <td>{table.SchemaName || '-'}</td>
                      <td style={{ fontWeight: 600 }}>{table.TableName}</td>
                      <td>{table.Columns.length}</td>
                      <td>{table.ForeignKeys.length}</td>
                      <td>{table.Indexes.length}</td>
                      <td className="table-context-preview">
                        {table.Context ? table.Context : <span className="muted-text">No context saved.</span>}
                      </td>
                      <td className="actions-column">
                        <button
                          className="btn-secondary compact-button table-build-action"
                          onClick={() => buildTableContext(table)}
                          disabled={!canBuild || !table.TableId || rowBuilding || buildingAll}
                          title={rowBuilding ? 'Building table context' : 'Build table context'}
                        >
                          <ButtonBusyContent Busy={rowBuilding} Label="Build Context" BusyLabel="Building table context" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <RecordViewModal
        Open={viewTable != null}
        Title={viewTable ? `${viewTable.SchemaName}.${viewTable.TableName}` : 'Table'}
        Subtitle={detail ? detail.Name || detail.DatabaseName || detail.DatabaseId : null}
        Rows={viewTable ? tableRows(viewTable) : []}
        OnClose={() => setViewTable(null)}
        Actions={viewTable?.TableId ? (
          <button
            type="button"
            className="btn-primary table-build-action"
            onClick={() => buildTableContext(viewTable)}
            disabled={!canBuild || buildingAll || buildingTableIds.has(viewTable.TableId)}
            title={buildingTableIds.has(viewTable.TableId) ? 'Building table context' : 'Build table context'}
          >
            <ButtonBusyContent Busy={buildingTableIds.has(viewTable.TableId)} Label="Build Context" BusyLabel="Building table context" />
          </button>
        ) : null}
      />
    </div>
  );
}

function defaultTableContextPrompt() {
  return 'For each selected table, produce concise durable context describing the table purpose, key columns, primary keys, relationships and join paths, common filters, caveats, and how it is typically used. Clearly label inferred relationships and do not include secrets or raw data.';
}

function selectAvailableProviderId(options: ChatOptionsResponse) {
  if (options.DefaultProviderId && options.Providers.some(provider => provider.Id === options.DefaultProviderId)) {
    return options.DefaultProviderId;
  }

  return options.Providers[0]?.Id || '';
}

function clampConcurrency(value?: number) {
  if (!Number.isFinite(value)) return 1;
  return Math.max(1, Math.min(16, Math.floor(value || 1)));
}

async function readJsonResponse<T>(response: Response, fallbackMessage: string): Promise<T> {
  const text = await response.text();
  if (!text) throw new Error(fallbackMessage);

  try {
    return JSON.parse(text) as T;
  } catch {
    throw new Error(text || fallbackMessage);
  }
}
