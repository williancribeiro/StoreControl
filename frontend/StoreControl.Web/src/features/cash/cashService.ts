import { httpClient } from '../../shared/api/httpClient'
import type {
  CashOperationResult,
  CashRegister,
  CashSessionDetails,
  CashSessionQuery,
  PagedCashSessions,
} from './cashTypes'

export async function listCashRegisters(storeId?: number, signal?: AbortSignal) {
  const response = await httpClient.get<CashRegister[]>('/api/cash/registers', {
    params: storeId ? { storeId } : undefined,
    signal,
  })
  return response.data
}

export async function getCurrentCashSession(cashRegisterId: number, signal?: AbortSignal) {
  const response = await httpClient.get<CashSessionDetails | undefined>('/api/cash/sessions/current', {
    params: { cashRegisterId },
    signal,
  })
  return response.status === 204 ? undefined : response.data
}

export async function listCashSessions(query: CashSessionQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedCashSessions>('/api/cash/sessions', {
    params: Object.fromEntries(
      Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
    ),
    signal,
  })
  return response.data
}

export async function getCashSession(cashSessionId: number, signal?: AbortSignal) {
  const response = await httpClient.get<CashSessionDetails>(`/api/cash/sessions/${cashSessionId}`, {
    signal,
  })
  return response.data
}

export async function openCashSession(
  operationId: string,
  cashRegisterId: number,
  openingAmount: number,
) {
  const response = await httpClient.post<CashOperationResult>(
    '/api/cash/sessions',
    { cashRegisterId, openingAmount },
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

export async function addCashMovement(
  cashSessionId: number,
  operationId: string,
  input: { rowVersion: string; type: 'CashIn' | 'CashOut'; amount: number; reason: string },
) {
  const response = await httpClient.post<CashOperationResult>(
    `/api/cash/sessions/${cashSessionId}/movements`,
    input,
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

export async function closeCashSession(
  cashSessionId: number,
  rowVersion: string,
  declaredClosingAmount: number,
) {
  const response = await httpClient.post<CashSessionDetails>(
    `/api/cash/sessions/${cashSessionId}/close`,
    { rowVersion, declaredClosingAmount },
  )
  return response.data
}
