import { httpClient } from '../../shared/api/httpClient'
import type {
  CreatePurchaseInput,
  PagedPurchases,
  PurchaseOrderDetails,
  PurchaseQuery,
  ReceivePurchaseInput,
  ReceivePurchaseResult,
} from './purchaseTypes'

export async function listPurchases(query: PurchaseQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedPurchases>('/api/purchases', {
    params: Object.fromEntries(
      Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
    ),
    signal,
  })
  return response.data
}

export async function getPurchase(purchaseOrderId: number, signal?: AbortSignal) {
  const response = await httpClient.get<PurchaseOrderDetails>(`/api/purchases/${purchaseOrderId}`, {
    signal,
  })
  return response.data
}

export async function createPurchase(input: CreatePurchaseInput) {
  const response = await httpClient.post<PurchaseOrderDetails>('/api/purchases', input)
  return response.data
}

export async function submitPurchase(purchaseOrderId: number, rowVersion: string) {
  const response = await httpClient.post<PurchaseOrderDetails>(
    `/api/purchases/${purchaseOrderId}/submit`,
    { rowVersion },
  )
  return response.data
}

export async function receivePurchase(
  purchaseOrderId: number,
  receiptId: string,
  input: ReceivePurchaseInput,
) {
  const response = await httpClient.post<ReceivePurchaseResult>(
    `/api/purchases/${purchaseOrderId}/receipts`,
    input,
    { headers: { 'Idempotency-Key': receiptId } },
  )
  return response.data
}

export async function finalizePurchase(purchaseOrderId: number, rowVersion: string) {
  const response = await httpClient.post<PurchaseOrderDetails>(
    `/api/purchases/${purchaseOrderId}/finalize`,
    { rowVersion },
  )
  return response.data
}

export async function cancelPurchase(purchaseOrderId: number, rowVersion: string, reason: string) {
  const response = await httpClient.post<PurchaseOrderDetails>(
    `/api/purchases/${purchaseOrderId}/cancel`,
    { rowVersion, reason },
  )
  return response.data
}
