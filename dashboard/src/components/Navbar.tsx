import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { getDisplayServerUrl } from '../api/client';
import { dashboardLanguages, getLanguage, setLanguage, translateTooltip, type DashboardLanguage } from '../i18n';
import iconImg from '../assets/icon.png';

export default function Navbar() {
  const navigate = useNavigate();
  const [serverUrl, setServerUrl] = useState('');
  const [language, setCurrentLanguage] = useState<DashboardLanguage>(() => getLanguage());
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
    getDisplayServerUrl().then(setServerUrl);
  }, []);

  function handleLogout() {
    sessionStorage.removeItem('tablix_api_key');
    navigate('/login');
  }

  function handleLanguageChanged(nextLanguage: DashboardLanguage) {
    setLanguage(nextLanguage);
    setCurrentLanguage(nextLanguage);
  }

  const iconButtonStyle = {
    width: '32px',
    height: '32px',
    padding: '0',
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
  };

  return (
    <nav className="topbar" style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      zIndex: 100,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '0 20px',
      height: '52px',
      minHeight: '52px',
      maxHeight: '52px',
      flex: '0 0 52px',
      overflow: 'hidden',
      backgroundColor: 'var(--bg-primary)',
      borderBottom: '1px solid var(--border-color)',
      boxShadow: 'var(--shadow)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
        <img src={iconImg} alt="Tablix" title={translateTooltip('nav.brand', language)} style={{ height: '28px' }} />
        <Link to="/" title={translateTooltip('nav.brand', language)} style={{ fontWeight: 600, fontSize: '16px', color: 'var(--text-primary)', textDecoration: 'none' }}>Tablix</Link>
        <Link to="/" title={translateTooltip('nav.databases', language)} style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Databases</Link>
        <Link to="/tables" title={translateTooltip('nav.tables', language)} style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Tables</Link>
        <Link to="/query" title={translateTooltip('nav.query', language)} style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Query</Link>
        <Link to="/chat" title={translateTooltip('nav.chat', language)} style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Chat</Link>
        <Link to="/models" title={translateTooltip('nav.models', language)} style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Models</Link>
        <Link to="/settings" title={translateTooltip('nav.settings', language)} style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>Settings</Link>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
        <span className="muted-text" title={translateTooltip('nav.server', language)} style={{ fontSize: '12px', maxWidth: '280px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{serverUrl}</span>

        <a
          href="https://discord.gg/tRAN8HgvK5"
          target="_blank"
          rel="noopener noreferrer"
          title="Join the Tablix Discord"
          aria-label="Join the Tablix Discord"
          className="btn-secondary"
          style={iconButtonStyle}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="var(--text-muted)">
            <path d="M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.055c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1568-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1567 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189Z" />
          </svg>
        </a>

        <a
          href="https://github.com/jchristn/Tablix"
          target="_blank"
          rel="noopener noreferrer"
          title={translateTooltip('nav.github', language)}
          aria-label="View Tablix on GitHub"
          className="btn-secondary"
          style={iconButtonStyle}
        >
          <svg width="18" height="18" viewBox="0 0 16 16" fill="var(--text-muted)">
            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
          </svg>
        </a>

        <select
          className="language-select"
          value={language}
          title={translateTooltip('nav.language', language)}
          aria-label="Dashboard help language"
          onChange={event => handleLanguageChanged(event.target.value as DashboardLanguage)}
        >
          {dashboardLanguages.map(option => (
            <option key={option.Code} value={option.Code}>{option.NativeLabel}</option>
          ))}
        </select>

        <button
          className="btn-secondary"
          onClick={() => setIsDark(!isDark)}
          aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
          title={translateTooltip('nav.theme', language)}
          style={iconButtonStyle}
        >
          {isDark ? (
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="var(--text-muted)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="4" />
              <path d="M12 2v2" />
              <path d="M12 20v2" />
              <path d="m4.93 4.93 1.41 1.41" />
              <path d="m17.66 17.66 1.41 1.41" />
              <path d="M2 12h2" />
              <path d="M20 12h2" />
              <path d="m6.34 17.66-1.41 1.41" />
              <path d="m19.07 4.93-1.41 1.41" />
            </svg>
          ) : (
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="var(--text-muted)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 3a6 6 0 0 0 9 7.5A9 9 0 1 1 12 3Z" />
            </svg>
          )}
        </button>

        <button
          className="btn-secondary"
          onClick={handleLogout}
          aria-label="Sign out"
          title={translateTooltip('nav.logout', language)}
          style={iconButtonStyle}
        >
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="var(--text-muted)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <path d="M16 17l5-5-5-5" />
            <path d="M21 12H9" />
          </svg>
        </button>
      </div>
    </nav>
  );
}
