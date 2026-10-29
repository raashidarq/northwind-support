import { useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { saveSession, useSession } from '../../auth/session';
import { Field } from '../../components/Field';

/** Agents sign in here, then go back to the page they were trying to open. */
export function LoginPage() {
  const session = useSession();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const returnTo = safeReturnTo(searchParams.get('returnTo'));

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [serverError, setServerError] = useState<string>();
  const [isSigningIn, setIsSigningIn] = useState(false);

  if (session) {
    return <Navigate to={returnTo} replace />;
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setServerError(undefined);

    const found: typeof errors = {};

    if (!email.trim().includes('@')) {
      found.email = 'Enter your work email address.';
    }

    if (password === '') {
      found.password = 'Enter your password.';
    }

    setErrors(found);

    if (found.email || found.password) {
      return;
    }

    setIsSigningIn(true);

    try {
      saveSession(await authApi.login({ email: email.trim(), password }));
      void navigate(returnTo, { replace: true });
    } catch (caught) {
      setServerError(toErrorMessage(caught, 'Could not sign in.'));
      setIsSigningIn(false);
    }
  };

  return (
    <main className="app__main">
      <div className="card login">
        <h1>Sign in</h1>

        <form onSubmit={(event) => void submit(event)} noValidate>
          <Field id="login-email" label="Email" error={errors.email}>
            {(props) => (
              <input
                {...props}
                type="email"
                autoComplete="username"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(props) => (
              <input
                {...props}
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            )}
          </Field>

          {serverError && (
            <p className="field__error" role="alert">
              {serverError}
            </p>
          )}

          <button type="submit" className="button button--primary" disabled={isSigningIn}>
            {isSigningIn ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </div>
    </main>
  );
}

/** Only a path inside this app, so a crafted link cannot send the agent to another site. */
function safeReturnTo(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/tickets';
}
