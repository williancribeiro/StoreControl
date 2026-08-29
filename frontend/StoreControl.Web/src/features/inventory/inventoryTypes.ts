import type { PagedResult } from '../registrations/registrationTypes'

export type StockPosition = {
  storeId: number
  storeCode: string
  storeName: string
  productId: number
  productCode: string
  productBarcode: string | null
  productName: string
  productIsActive: boolean
  quantityOnHand: number
  minimumQuantity: number
  isBelowMinimum: boolean
  rowVersion: string | null
}

export type StockMovement = {
  id: number
  storeId: number
  storeCode: string
  storeName: string
  productId: number
  productCode: string
  productName: string
  type: StockMovementType
  quantityChange: number
  quantityBefore: number
  quantityAfter: number
  reason: string
  createdByUserId: string
  createdByUserName: string
  occurredAtUtc: string
  correlationId: string
  referenceType: string | null
  referenceId: string | null
}

export type StockMovementType =
  | 'PurchaseReceipt'
  | 'Sale'
  | 'PositiveAdjustment'
  | 'NegativeAdjustment'
  | 'TransferIn'
  | 'TransferOut'
  | 'SaleReversal'
  | 'OtherIn'
  | 'OtherOut'
  | 'InitialBalance'

export type PositionQuery = {
  page: number
  pageSize: number
  search?: string
  storeId?: number
  productId?: number
  belowMinimum?: boolean
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type MovementQuery = {
  page: number
  pageSize: number
  search?: string
  storeId?: number
  productId?: number
  type?: StockMovementType
  fromUtc?: string
  toUtc?: string
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type StockAdjustmentResult = {
  position: StockPosition
  movement: StockMovement
}

export type PagedPositions = PagedResult<StockPosition>
export type PagedMovements = PagedResult<StockMovement>

export const movementLabels: Record<StockMovementType, string> = {
  PurchaseReceipt: 'Entrada por compra',
  Sale: 'Saída por venda',
  PositiveAdjustment: 'Ajuste positivo',
  NegativeAdjustment: 'Ajuste negativo',
  TransferIn: 'Entrada por transferência',
  TransferOut: 'Saída por transferência',
  SaleReversal: 'Estorno de venda',
  OtherIn: 'Outra entrada',
  OtherOut: 'Outra saída',
  InitialBalance: 'Saldo inicial',
}
