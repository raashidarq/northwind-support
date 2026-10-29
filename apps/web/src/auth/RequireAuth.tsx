import { Navigate, useLocation } from 'react-router-dom';
import { useSession } from './session';

/**
 * Shows its children only to a signed-in agent. Anyone else goes to the login page, which
 * brings them back here afterwards. Also fires when a 401 clears the session mid-visit.
 */
export function RequireAuth({ children }: { children: React.ReactNode }) {
  const session = useSession();
  const location = useLocation();

  if (!session) {
    const returnTo = encodeURIComponent(location.pathname + location.search);

    return <Navigate to={`/login?returnTo=${returnTo}`} replace />;
  }

  return children;
}
