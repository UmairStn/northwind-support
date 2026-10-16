import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/useAuth';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { user, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        {isAuthenticated && (
          <nav className="app__nav" aria-label="Main">
            <NavLink to="/tickets">Tickets</NavLink>
            <NavLink to="/customers">Customers</NavLink>
          </nav>
        )}

        {isAuthenticated && user && (
          <div className="app__user" style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <span style={{ fontSize: '0.875rem', fontWeight: 500 }}>
              👤 {user.fullName}
            </span>
            <button
              type="button"
              className="button button--secondary button--sm"
              onClick={handleLogout}
            >
              Sign Out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
