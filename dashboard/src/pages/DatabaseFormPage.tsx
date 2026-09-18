import { useParams, useNavigate } from 'react-router-dom';
import DatabaseFormModal from '../components/DatabaseFormModal';

/**
 * Deep-link wrapper for the database create/edit routes. The edit experience is a modal;
 * this keeps /databases/new and /databases/:id/edit working by presenting that modal and
 * returning to the relevant page on close or save.
 */
export default function DatabaseFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();

  return (
    <DatabaseFormModal
      Open={true}
      DatabaseId={id ?? null}
      OnClose={() => navigate(isEdit ? `/databases/${id}` : '/')}
      OnSaved={savedId => navigate(isEdit ? `/databases/${id}` : `/databases/${savedId}`)}
    />
  );
}
