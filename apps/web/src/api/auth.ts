import type { LoginPayload, LoginResponse } from '../types/api';
import { request } from './client';

export const authApi = {
  login: (payload: LoginPayload) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
};
