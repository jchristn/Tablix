import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Layout from './components/Layout';
import LoginPage from './pages/LoginPage';
import DatabaseListPage from './pages/DatabaseListPage';
import DatabaseDetailPage from './pages/DatabaseDetailPage';
import DatabaseFormPage from './pages/DatabaseFormPage';
import QueryPage from './pages/QueryPage';
import ChatPage from './pages/ChatPage';
import SettingsPage from './pages/SettingsPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<Layout />}>
          <Route path="/" element={<DatabaseListPage />} />
          <Route path="/databases/new" element={<DatabaseFormPage />} />
          <Route path="/databases/:id" element={<DatabaseDetailPage />} />
          <Route path="/databases/:id/edit" element={<DatabaseFormPage />} />
          <Route path="/query" element={<QueryPage />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
