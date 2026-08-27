import { httpClient } from '../../shared/api/httpClient'

export type SmokeResponse = {
  status: string
  application: string
  apiUtcTime: string
  database: {
    status: string
    serverUtcTime: string
    foundationVersion: string
  }
}

export async function getSmokeStatus(signal?: AbortSignal) {
  const response = await httpClient.get<SmokeResponse>('/api/smoke', { signal })
  return response.data
}
