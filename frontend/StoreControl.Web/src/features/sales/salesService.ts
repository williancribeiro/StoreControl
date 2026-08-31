import { httpClient } from '../../shared/api/httpClient'
import type {
  CancelSaleInput,
  CancelSaleResult,
  FinalizeSaleInput,
  FinalizeSaleResult,
  PagedSales,
  SaleDetails,
  SaleQuery,
} from './salesTypes'

export async function listSales(query: SaleQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedSales>('/api/sales', {
    params: Object.fromEntries(
      Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
    ),
    signal,
  })
  return response.data
}

export async function cancelSale(
  saleId: number,
  operationId: string,
  input: CancelSaleInput,
) {
  const response = await httpClient.post<CancelSaleResult>(
    `/api/sales/${saleId}/cancellation`,
    input,
    { headers: { 'Idempotency-Key': operationId } },
  )
  return response.data
}

export async function getSale(saleId: number, signal?: AbortSignal) {
  const response = await httpClient.get<SaleDetails>(`/api/sales/${saleId}`, { signal })
  return response.data
}

export async function finalizeSale(operationId: string, input: FinalizeSaleInput) {
  const response = await httpClient.post<FinalizeSaleResult>('/api/sales', input, {
    headers: { 'Idempotency-Key': operationId },
  })
  return response.data
}
