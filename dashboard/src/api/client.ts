import type { RuntimeConfig } from '../types';

let cachedConfig: RuntimeConfig | null = null;

export async function getServerUrl(): Promise<string> {
  const override = getServerUrlOverride();
  if (override) return override;

  const config = await getRuntimeConfig();
  return config.serverUrl ?? '';
}

export async function getDisplayServerUrl(): Promise<string> {
  const override = getServerUrlOverride();
  if (override) return override;

  const config = await getRuntimeConfig();
  return config.displayServerUrl || config.serverUrl || 'http://localhost:9100';
}

export function setServerUrlOverride(serverUrl: string) {
  const trimmed = serverUrl.trim();
  if (trimmed.length > 0) {
    localStorage.setItem('tablix_server_url', trimmed);
  } else {
    localStorage.removeItem('tablix_server_url');
  }
}

function getServerUrlOverride() {
  return localStorage.getItem('tablix_server_url') || '';
}

async function getRuntimeConfig(): Promise<RuntimeConfig> {
  if (cachedConfig) return cachedConfig;

  // Try fetched runtime config first (for Docker deployments)
  try {
    const response = await fetch('/config.json');
    if (response.ok) {
      cachedConfig = await response.json();
      if (cachedConfig) return cachedConfig;
    }
  } catch {
    // fall through
  }

  // Fall back to Vite env var (for development)
  const envUrl = import.meta.env.VITE_TABLIX_SERVER_URL || import.meta.env.TABLIX_SERVER_URL;
  if (envUrl) {
    cachedConfig = { serverUrl: envUrl, displayServerUrl: envUrl };
    return cachedConfig;
  }

  // Default
  cachedConfig = { serverUrl: 'http://localhost:9100', displayServerUrl: 'http://localhost:9100' };
  return cachedConfig;
}

export async function apiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const baseUrl = await getServerUrl();
  const apiKey = sessionStorage.getItem('tablix_api_key') || '';

  const headers = new Headers(options.headers);

  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  if (apiKey) {
    headers.set('Authorization', `Bearer ${apiKey}`);
  }

  return fetch(`${baseUrl}${path}`, {
    ...options,
    headers,
  });
}
