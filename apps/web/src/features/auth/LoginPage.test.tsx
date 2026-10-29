import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { getSession } from '../../auth/session';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn() } }));

const login = vi.mocked(authApi.login);

function renderAt(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/tickets/7" element={<p>Ticket 7</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    login.mockReset();
  });

  it('checks the fields before calling the API', async () => {
    const user = userEvent.setup();
    renderAt('/login');

    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByText('Enter your work email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('shows the server\'s message when the credentials are wrong', async () => {
    login.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    const user = userEvent.setup();
    renderAt('/login');

    await user.type(screen.getByLabelText('Email'), 'sara@example.com');
    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Email or password is incorrect.'));
    expect(getSession()).toBeNull();
  });

  it('signs in and goes back to the page the agent wanted', async () => {
    login.mockResolvedValue({
      token: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60 * 60_000).toISOString(),
      agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.com' },
    });
    const user = userEvent.setup();
    renderAt(`/login?returnTo=${encodeURIComponent('/tickets/7')}`);

    await user.type(screen.getByLabelText('Email'), 'sara@example.com');
    await user.type(screen.getByLabelText('Password'), 'Northwind-Dev-2026');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Ticket 7')).toBeInTheDocument();
    expect(getSession()?.agent.fullName).toBe('Sara Lindqvist');
  });
});
