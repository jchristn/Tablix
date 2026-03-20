import { Outlet, Navigate } from 'react-router-dom';
import Navbar from './Navbar';

export default function Layout() {
  const apiKey = sessionStorage.getItem('tablix_api_key');
  if (!apiKey) return <Navigate to="/login" replace />;

  return (
    <div>
      <Navbar />
      <main style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
        <Outlet />
      </main>
    </div>
  );
}
