import axios, { type InternalAxiosRequestConfig } from 'axios'

type RetryableRequest = InternalAxiosRequestConfig & { _authRetry?: boolean }

let accessToken: string | null = null
let refreshHandler: (() => Promise<string | null>) | null = null
let refreshInFlight: Promise<string | null> | null = null

export const httpClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  timeout: 10_000,
  withCredentials: true,
  headers: {
    Accept: 'application/json',
  },
})

httpClient.interceptors.request.use((config) => {
  config.headers.set('X-Correlation-ID', crypto.randomUUID())
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`)
  }
  return config
})

httpClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 401 || !error.config) {
      return Promise.reject(error)
    }

    const request = error.config as RetryableRequest
    const requestUrl = request.url ?? ''
    if (
      request._authRetry ||
      requestUrl.includes('/api/auth/login') ||
      requestUrl.includes('/api/auth/refresh')
    ) {
      return Promise.reject(error)
    }

    request._authRetry = true
    if (!refreshHandler) {
      return Promise.reject(error)
    }

    refreshInFlight ??= refreshHandler().finally(() => {
      refreshInFlight = null
    })
    const refreshedToken = await refreshInFlight
    if (!refreshedToken) {
      return Promise.reject(error)
    }

    request.headers.set('Authorization', `Bearer ${refreshedToken}`)
    return httpClient.request(request)
  },
)

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function setRefreshHandler(handler: (() => Promise<string | null>) | null) {
  refreshHandler = handler
}
