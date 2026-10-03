import { useEffect, useState } from 'react';
import ClipboardButton from './ClipboardButton';
import { translateTooltip } from '../i18n';

type ServiceStatus = 'checking' | 'reachable' | 'unreachable';

interface ExternalService {
  Key: string;
  Name: string;
  Purpose: string;
  Port: number;
  Path: string;
  Username?: string;
  Password?: string;
}

// Host-published ports from docker/compose.yaml. URLs use the hostname the browser already reached the dashboard
// on, so they stay correct on the machine running the stack.
const services: ExternalService[] = [
  { Key: 'grafana', Name: 'Grafana', Purpose: 'Dashboards: overview, HTTP, chat, crawl, queries, integrations, runtime, logs and traces', Port: 3000, Path: '/dashboards?tag=tablix', Username: 'admin', Password: 'admin' },
  { Key: 'prometheus', Name: 'Prometheus', Purpose: 'Metrics store and PromQL query UI', Port: 9090, Path: '/' },
  { Key: 'tempo', Name: 'Tempo', Purpose: 'Trace store API (browse traces from Grafana)', Port: 3200, Path: '/ready' },
  { Key: 'loki', Name: 'Loki', Purpose: 'Log store API (browse logs from Grafana)', Port: 3100, Path: '/ready' },
  { Key: 'collector', Name: 'OpenTelemetry Collector', Purpose: 'OTLP intake for traces and logs (gRPC 4317, HTTP 4318)', Port: 4318, Path: '/' }
];

function serviceUrl(service: ExternalService) {
  const hostname = window.location.hostname || 'localhost';
  return `http://${hostname}:${service.Port}${service.Path}`;
}

async function probe(url: string): Promise<ServiceStatus> {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), 3000);
  try {
    // no-cors resolves with an opaque response whenever something answers, which is all reachability needs.
    await fetch(url, { mode: 'no-cors', cache: 'no-store', signal: controller.signal });
    return 'reachable';
  } catch {
    return 'unreachable';
  } finally {
    window.clearTimeout(timer);
  }
}

async function probeAll(): Promise<Record<string, ServiceStatus>> {
  const results = await Promise.all(services.map(async service => [service.Key, await probe(serviceUrl(service))] as const));
  return Object.fromEntries(results);
}

export default function ExternalServicesCard() {
  const [statuses, setStatuses] = useState<Record<string, ServiceStatus>>({});

  useEffect(() => {
    let active = true;
    probeAll().then(results => { if (active) setStatuses(results); });
    return () => { active = false; };
  }, []);

  async function runProbes() {
    setStatuses(await probeAll());
  }

  function refresh() {
    setStatuses({});
    void runProbes();
  }

  const anyReachable = Object.values(statuses).some(status => status === 'reachable');
  const allChecked = services.every(service => statuses[service.Key] && statuses[service.Key] !== 'checking');

  return (
    <div className="card external-services-card">
      <div className="table-list-header">
        <div>
          <h3 title={translateTooltip('services.card')}>External Services</h3>
          <span className="muted-text">Observability tools bundled with the Docker stack. Default credentials are for local development only.</span>
        </div>
        <div className="table-list-controls">
          <button type="button" className="btn-secondary" onClick={refresh} title={translateTooltip('services.refresh')}>
            Refresh
          </button>
        </div>
      </div>

      {allChecked && !anyReachable && (
        <p className="muted-text external-services-note">
          None of these services answered from this browser. They run only when the observability stack in docker/compose.yaml is up, and they are published on the host loopback address by default.
        </p>
      )}

      <table className="data-table wide-table">
        <thead>
          <tr>
            <th title="Bundled service">Service</th>
            <th title="Browser-reachable URL">URL</th>
            <th title="Default sign-in for local development">Credentials</th>
            <th title="Reachability from this browser">Status</th>
          </tr>
        </thead>
        <tbody>
          {services.map(service => {
            const url = serviceUrl(service);
            const status = statuses[service.Key] || 'checking';
            return (
              <tr key={service.Key}>
                <td title={service.Purpose}>
                  <div>{service.Name}</div>
                  <div className="muted-text external-service-purpose">{service.Purpose}</div>
                </td>
                <td>
                  <span className="external-service-url">
                    <a href={url} target="_blank" rel="noreferrer" className="mono-token" title={translateTooltip(`services.${service.Key}`)}>{url}</a>
                    <ClipboardButton text={url} title={translateTooltip('common.copy')} label={`Copy ${service.Name} URL`} />
                  </span>
                </td>
                <td>
                  {service.Username ? (
                    <span className="external-service-credentials">
                      <code className="mono-token" title="Default username">{service.Username}</code>
                      <span className="muted-text">/</span>
                      <code className="mono-token" title="Default password">{service.Password}</code>
                    </span>
                  ) : (
                    <span className="muted-text" title="No authentication; keep this service on an internal network">None</span>
                  )}
                </td>
                <td>
                  <span
                    className={`badge ${status === 'reachable' ? 'badge-success' : status === 'unreachable' ? 'badge-danger' : 'badge-warning'}`}
                    title={translateTooltip(`services.status.${status}`)}
                  >
                    {status === 'reachable' ? 'Reachable' : status === 'unreachable' ? 'Unreachable' : 'Checking'}
                  </span>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
