import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';
import { setStoredSession } from '../../api/client';

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('redirects unauthenticated visitors to /login', () => {
    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Screen</div>} />
            <Route
              path="/tickets"
              element={
                <ProtectedRoute>
                  <div>Secret Ticket Area</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(screen.getByText('Login Page Screen')).toBeInTheDocument();
    expect(screen.queryByText('Secret Ticket Area')).not.toBeInTheDocument();
  });

  it('renders protected content when user is authenticated', () => {
    setStoredSession('valid-token', { id: 1, fullName: 'Alex Turner', email: 'alex@northwind.example' });

    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Screen</div>} />
            <Route
              path="/tickets"
              element={
                <ProtectedRoute>
                  <div>Secret Ticket Area</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(screen.getByText('Secret Ticket Area')).toBeInTheDocument();
    expect(screen.queryByText('Login Page Screen')).not.toBeInTheDocument();
  });
});
