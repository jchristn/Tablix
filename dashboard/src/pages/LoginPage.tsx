import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import logoImg from '../assets/logo.png';

export default function LoginPage() {
  const [apiKey, setApiKey] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!apiKey.trim()) {
      setError('API key is required.');
      return;
    }
    sessionStorage.setItem('tablix_api_key', apiKey.trim());
    navigate('/');
  }

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      minHeight: '100vh',
      backgroundColor: 'var(--bg-secondary)',
    }}>
      <div className="card" style={{ width: '380px', textAlign: 'center' }}>
        <img src={logoImg} alt="Tablix" title="Tablix — Database discovery and query platform" style={{ maxWidth: '240px', marginBottom: '24px' }} />

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <input
              type="password"
              placeholder="API Key"
              title="Enter an API key from the ApiKeys list in tablix.json"
              value={apiKey}
              onChange={e => { setApiKey(e.target.value); setError(''); }}
              autoFocus
            />
          </div>

          {error && <p className="error-text" style={{ marginBottom: '12px' }}>{error}</p>}

          <button type="submit" className="btn-primary" title="Authenticate with the provided API key" style={{ width: '100%' }}>
            Sign In
          </button>
        </form>

        <div style={{ marginTop: '20px', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '12px' }}>
          <a href="https://github.com/jchristn/Tablix" target="_blank" rel="noopener noreferrer" title="View Tablix on GitHub">
            <svg width="20" height="20" viewBox="0 0 16 16" fill="var(--text-muted)">
              <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
            </svg>
          </a>
        </div>
        <p className="muted-text" style={{ marginTop: '8px', fontSize: '11px' }}>&copy;2026 Joel Christner</p>
      </div>
    </div>
  );
}
