import type { PagedResult } from '../registrations/registrationTypes'

export type StockTransferStatus = 'Created' | 'InTransit' | 'Received' | 'Cancelled'

export type StockTransferSummary = {
  id: number
  number: string
  originStoreId: number
  originStoreCode: string
  originStoreName: string
  destinationStoreId: number
  destinationStoreCode: string
  destinationStoreName: string
  status: StockTransferStatus
  itemCount: number
  totalQuantity: number
  createdAtUtc: string
  dispatchedAtUtc: string | null
  receivedAtUtc: string | null
  rowVersion: string
}

export type StockTransferDetails = StockTransferSummary & {
  operationId: string
  createdByUserId: string
  createdByUserName: string
  notes: string | null
  dispatchOperationId: string | null
  dispatchedByUserId: string | null
  dispatchedByUserName: string | null
  receiptOperationId: string | null
  receivedByUserId: string | null
  receivedByUserName: string | null
  cancellationOperationId: string | null
  cancelledByUserId: string | null
  cancelledByUserName: string | null
  cancelledAtUtc: string | null
  cancellationReason: string | null
  items: Array<{
    id: number
    productId: number
    productCode: string
    productName: string
    quantity: number
  }>
  statusHistory: Array<{
    id: number
    status: StockTransferStatus
    changedByUserId: string
    changedByUserName: string
    changedAtUtc: string
    description: string
  }>
}

export type StockTransferQuery = {
  page: number
  pageSize: number
  search?: string
  originStoreId?: number
  destinationStoreId?: number
  status?: StockTransferStatus
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type CreateStockTransferInput = {
  originStoreId: number
  destinationStoreId: number
  notes?: string
  items: Array<{ productId: number; quantity: number }>
}

export type StockTransferOperationResult = {
  transfer: StockTransferDetails
  wasAlreadyProcessed: boolean
}

export type PagedStockTransfers = PagedResult<StockTransferSummary>

export const transferStatuses: StockTransferStatus[] = [
  'Created',
  'InTransit',
  'Received',
  'Cancelled',
]

export const transferStatusLabels: Record<StockTransferStatus, string> = {
  Created: 'Criada',
  InTransit: 'Em trânsito',
  Received: 'Recebida',
  Cancelled: 'Cancelada',
}
