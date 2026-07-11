import { Outlet, Navigate, useLocation } from 'react-router-dom';
import Navbar from './Navbar';
import SetupWizard from './SetupWizard';

export default function Layout() {
  const apiKey = sessionStorage.getItem('tablix_api_key');
  const location = useLocation();
  if (!apiKey) return <Navigate to="/login" replace />;

  const mainClassName = location.pathname === '/chat' ? 'app-main app-main-chat' : 'app-main';

  return (
    <div className="app-shell">
      <Navbar />
      <main className={mainClassName}>
        <Outlet />
      </main>
      <SetupWizard />
    </div>
  );
}
