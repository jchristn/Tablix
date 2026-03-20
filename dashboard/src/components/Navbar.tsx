import { useNavigate, Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { getServerUrl } from '../api/client';
import iconImg from '../assets/icon.png';

export default function Navbar() {
  const navigate = useNavigate();
  const [serverUrl, setServerUrl] = useState('');
  const [isDark, setIsDark] = useState(() => {
    const stored = localStorage.getItem('tablix_theme');
    if (stored) return stored === 'dark';
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
    localStorage.setItem('tablix_theme', isDark ? 'dark' : 'light');
  }, [isDark]);

  useEffect(() => {
    getServerUrl().then(setServerUrl);
  }, []);

  function handleLogout() {
    sessionStorage.removeItem('tablix_api_key');
    navigate('/login');
  }

  return (
    <nav style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '0 20px',
      height: '52px',
      backgroundColor: 'var(--bg-primary)',
      borderBottom: '1px solid var(--border-color)',
      boxShadow: 'var(--shadow)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
        <img src={iconImg} alt="Tablix" title="Tablix — Database discovery and query platform" style={{ height: '28px' }} />
        <Link to="/" title="Go to home page" style={{ fontWeight: 600, fontSize: '16px', color: 'var(--text-primary)', textDecoration: 'none' }}>Tablix</Link>
        <Link to="/" title="View and manage configured databases" style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Databases</Link>
        <Link to="/query" title="Execute SQL queries against a database" style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Query</Link>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
        <a href="https://github.com/jchristn/Tablix" target="_blank" rel="noopener noreferrer" title="View Tablix on GitHub">
          <svg width="18" height="18" viewBox="0 0 16 16" fill="var(--text-muted)">
            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
          </svg>
        </a>

        <span className="muted-text" title="Connected Tablix server URL" style={{ fontSize: '12px' }}>{serverUrl}</span>

        <button
          className="btn-secondary"
          onClick={() => setIsDark(!isDark)}
          title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
          style={{ padding: '4px 10px', fontSize: '13px' }}
        >
          {isDark ? 'Light' : 'Dark'}
        </button>

        <button
          className="btn-secondary"
          onClick={handleLogout}
          title="Sign out and return to the login page"
          style={{ padding: '4px 10px', fontSize: '13px' }}
        >
          Logout
        </button>
      </div>
    </nav>
  );
}
