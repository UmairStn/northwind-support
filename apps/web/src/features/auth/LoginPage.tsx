import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiError, toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import { useAuth } from './useAuth';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Preserve redirect destination from location state (AU-11)
  const from = (location.state as { from?: { pathname?: string } })?.from?.pathname || '/tickets';

  const validate = () => {
    const nextErrors: { email?: string; password?: string } = {};

    if (!email.trim()) {
      nextErrors.email = 'Email is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      nextErrors.email = 'Please enter a valid email address.';
    }

    if (!password) {
      nextErrors.password = 'Password is required.';
    }

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);

    try {
      await login({ email: email.trim(), password });
      navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        setServerError(err.message || 'Invalid email or password.');
      } else {
        setServerError(toErrorMessage(err, 'Unable to sign in. Please try again.'));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="card login-card">
        <h1>Sign In to Northwind Support</h1>
        <p className="login-subtitle">
          Please sign in with your agent credentials to access the support desk.
        </p>

        {serverError && (
          <div className="error" role="alert">
            <p style={{ margin: 0 }}>{serverError}</p>
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          <Field id="login-email" label="Email Address" error={errors.email}>
            {(props) => (
              <input
                {...props}
                type="email"
                className="input"
                autoComplete="email"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value);
                  if (errors.email) setErrors((prev) => ({ ...prev, email: undefined }));
                }}
                disabled={isSubmitting}
                placeholder="agent@northwind-support.example"
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(props) => (
              <input
                {...props}
                type="password"
                className="input"
                autoComplete="current-password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value);
                  if (errors.password) setErrors((prev) => ({ ...prev, password: undefined }));
                }}
                disabled={isSubmitting}
              />
            )}
          </Field>

          <div style={{ marginTop: '1.5rem' }}>
            <button
              type="submit"
              className="button button--primary"
              disabled={isSubmitting}
              style={{ width: '100%', justifyContent: 'center' }}
            >
              {isSubmitting ? 'Signing in…' : 'Sign In'}
            </button>
          </div>
        </form>

        <div
          className="summary__note"
          style={{ marginTop: '1.5rem', fontSize: '0.875rem' }}
        >
          <strong>Demo credentials:</strong>
          <br />
          Email: <code>alex.turner@northwind-support.example</code>
          <br />
          Password: <code>LocalDev123!</code>
        </div>
      </div>
    </div>
  );
}
