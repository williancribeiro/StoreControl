import { httpClient } from '../../shared/api/httpClient'
import type {
  CreateStockTransferInput,
  PagedStockTransfers,
  StockTransferDetails,
  StockTransferOperationResult,
  StockTransferQuery,
} from './transferTypes'

export async function listStockTransfers(query: StockTransferQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedStockTransfers>('/api/inventory/transfers', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function getStockTransfer(stockTransferId: number, signal?: AbortSignal) {
  const response = await httpClient.get<StockTransferDetails>(
    `/api/inventory/transfers/${stockTransferId}`,
    { signal },
  )
  return response.data
}

export async function createStockTransfer(
  operationId: string,
  input: CreateStockTransferInput,
) {
  const response = await httpClient.post<StockTransferOperationResult>(
    '/api/inventory/transfers',
    input,
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

export async function dispatchStockTransfer(
  stockTransferId: number,
  operationId: string,
  rowVersion: string,
) {
  const response = await httpClient.post<StockTransferOperationResult>(
    `/api/inventory/transfers/${stockTransferId}/dispatch`,
    { rowVersion },
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

export async function receiveStockTransfer(
  stockTransferId: number,
  operationId: string,
  rowVersion: string,
) {
  const response = await httpClient.post<StockTransferOperationResult>(
    `/api/inventory/transfers/${stockTransferId}/receive`,
    { rowVersion },
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

export async function cancelStockTransfer(
  stockTransferId: number,
  operationId: string,
  rowVersion: string,
  reason: string,
) {
  const response = await httpClient.post<StockTransferOperationResult>(
    `/api/inventory/transfers/${stockTransferId}/cancel`,
    { rowVersion, reason },
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

function cleanQuery(query: StockTransferQuery) {
  return Object.fromEntries(
    Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
  )
}
