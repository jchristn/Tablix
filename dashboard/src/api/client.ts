import type { RuntimeConfig } from '../types';

let cachedConfig: RuntimeConfig | null = null;

export async function getServerUrl(): Promise<string> {
  if (cachedConfig) return cachedConfig.serverUrl;

  // Try fetched runtime config first (for Docker deployments)
  try {
    const response = await fetch('/config.json');
    if (response.ok) {
      cachedConfig = await response.json();
      if (cachedConfig && cachedConfig.serverUrl) return cachedConfig.serverUrl;
    }
  } catch {
    // fall through
  }

  // Fall back to Vite env var (for development)
  const envUrl = import.meta.env.VITE_TABLIX_SERVER_URL;
  if (envUrl) {
    cachedConfig = { serverUrl: envUrl };
    return envUrl;
  }

  // Default
  cachedConfig = { serverUrl: 'http://localhost:9100' };
  return cachedConfig.serverUrl;
}

export async function apiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const baseUrl = await getServerUrl();
  const apiKey = sessionStorage.getItem('tablix_api_key') || '';

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> || {}),
  };

  if (apiKey) {
    headers['Authorization'] = `Bearer ${apiKey}`;
  }

  return fetch(`${baseUrl}${path}`, {
    ...options,
    headers,
  });
}
