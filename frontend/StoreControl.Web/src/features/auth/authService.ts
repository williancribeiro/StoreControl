import { httpClient } from '../../shared/api/httpClient'
import type { AuthResponse } from './authTypes'

let sharedRefresh: Promise<AuthResponse | null> | null = null

export async function login(email: string, password: string) {
  const response = await httpClient.post<AuthResponse>('/api/auth/login', { email, password })
  return response.data
}

export function refreshSession() {
  sharedRefresh ??= httpClient
    .post<AuthResponse>('/api/auth/refresh')
    .then((response) => response.data)
    .catch(() => null)
    .finally(() => {
      sharedRefresh = null
    })

  return sharedRefresh
}

export async function logout() {
  await httpClient.post('/api/auth/logout')
}
