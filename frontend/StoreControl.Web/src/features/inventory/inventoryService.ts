import { httpClient } from '../../shared/api/httpClient'
import type {
  MovementQuery,
  PagedMovements,
  PagedPositions,
  PositionQuery,
  StockAdjustmentResult,
  StockPosition,
} from './inventoryTypes'

export async function listStockPositions(query: PositionQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedPositions>('/api/inventory/balances', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function getStockPosition(storeId: number, productId: number, signal?: AbortSignal) {
  const response = await httpClient.get<StockPosition>(
    `/api/inventory/balances/${storeId}/${productId}`,
    { signal },
  )
  return response.data
}

export async function updateMinimumStock(
  storeId: number,
  productId: number,
  minimumQuantity: number,
  rowVersion: string | null,
) {
  const response = await httpClient.put<StockPosition>(
    `/api/inventory/balances/${storeId}/${productId}/minimum`,
    { minimumQuantity, rowVersion },
  )
  return response.data
}

export async function listStockMovements(query: MovementQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedMovements>('/api/inventory/movements', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function createStockAdjustment(input: {
  storeId: number
  productId: number
  direction: 'positive' | 'negative'
  quantity: number
  reason: string
  rowVersion: string | null
}) {
  const response = await httpClient.post<StockAdjustmentResult>('/api/inventory/adjustments', input)
  return response.data
}

function cleanQuery<T extends PositionQuery | MovementQuery>(query: T) {
  return Object.fromEntries(
    Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
  )
}
