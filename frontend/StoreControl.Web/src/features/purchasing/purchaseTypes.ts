import type { PagedResult } from '../registrations/registrationTypes'

export type PurchaseStatus =
  | 'Created'
  | 'Pending'
  | 'PartiallyReceived'
  | 'Received'
  | 'Finalized'
  | 'Cancelled'

export type PurchaseOrderSummary = {
  id: number
  number: string
  storeId: number
  storeCode: string
  storeName: string
  supplierId: number
  supplierCode: string
  supplierName: string
  status: PurchaseStatus
  itemCount: number
  totalAmount: number
  createdAtUtc: string
  expectedDate: string | null
  rowVersion: string
}

export type PurchaseOrderItem = {
  id: number
  productId: number
  productCode: string
  productName: string
  orderedQuantity: number
  receivedQuantity: number
  remainingQuantity: number
  unitCost: number
  lineTotal: number
}

export type PurchaseReceipt = {
  id: string
  receivedAtUtc: string
  receivedByUserId: string
  receivedByUserName: string
  notes: string | null
  items: Array<{
    purchaseOrderItemId: number
    productId: number
    productCode: string
    productName: string
    quantity: number
  }>
}

export type PurchaseStatusHistory = {
  id: number
  status: PurchaseStatus
  changedAtUtc: string
  changedByUserId: string
  changedByUserName: string
  description: string
}

export type PurchaseOrderDetails = PurchaseOrderSummary & {
  createdByUserId: string
  createdByUserName: string
  updatedAtUtc: string
  notes: string | null
  items: PurchaseOrderItem[]
  receipts: PurchaseReceipt[]
  statusHistory: PurchaseStatusHistory[]
}

export type PurchaseQuery = {
  page: number
  pageSize: number
  search?: string
  storeId?: number
  supplierId?: number
  status?: PurchaseStatus
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type PagedPurchases = PagedResult<PurchaseOrderSummary>

export type CreatePurchaseInput = {
  storeId: number
  supplierId: number
  expectedDate?: string
  notes?: string
  items: Array<{ productId: number; quantity: number; unitCost: number }>
}

export type ReceivePurchaseInput = {
  rowVersion: string
  notes?: string
  items: Array<{ purchaseOrderItemId: number; quantity: number }>
}

export type ReceivePurchaseResult = {
  purchase: PurchaseOrderDetails
  receiptId: string
  wasAlreadyProcessed: boolean
}

export const purchaseStatusLabels: Record<PurchaseStatus, string> = {
  Created: 'Criada',
  Pending: 'Pendente',
  PartiallyReceived: 'Recebida parcialmente',
  Received: 'Recebida',
  Finalized: 'Finalizada',
  Cancelled: 'Cancelada',
}

export const purchaseStatuses = Object.keys(purchaseStatusLabels) as PurchaseStatus[]
