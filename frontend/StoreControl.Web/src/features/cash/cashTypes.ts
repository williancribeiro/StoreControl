import type { PagedResult } from '../registrations/registrationTypes'
import type { PaymentMethod } from '../sales/salesTypes'

export type CashRegister = {
  id: number
  storeId: number
  storeCode: string
  storeName: string
  code: string
  name: string
  isActive: boolean
}

export type CashSessionStatus = 'Open' | 'Closed'

export type CashMovement = {
  id: number
  type: 'Opening' | 'SalePayment' | 'CashIn' | 'CashOut' | 'SaleRefund'
  paymentMethod: PaymentMethod | null
  amount: number
  reason: string
  createdByUserId: string
  createdByUserName: string
  occurredAtUtc: string
  correlationId: string
  referenceType: string | null
  referenceId: string | null
}

export type CashSessionSummary = {
  id: number
  cashRegisterId: number
  cashRegisterCode: string
  storeId: number
  storeCode: string
  storeName: string
  status: CashSessionStatus
  openedAtUtc: string
  closedAtUtc: string | null
  openedByUserName: string
  openingAmount: number
  expectedCashAmount: number
  declaredClosingAmount: number | null
  differenceAmount: number | null
  rowVersion: string
}

export type CashSessionDetails = CashSessionSummary & {
  operationId: string
  cashRegisterName: string
  openedByUserId: string
  closedByUserId: string | null
  closedByUserName: string | null
  movements: CashMovement[]
}

export type CashOperationResult = {
  session: CashSessionDetails
  wasAlreadyProcessed: boolean
}

export type CashSessionQuery = {
  page: number
  pageSize: number
  storeId?: number
  cashRegisterId?: number
  status?: CashSessionStatus
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type PagedCashSessions = PagedResult<CashSessionSummary>

export const cashMovementLabels: Record<CashMovement['type'], string> = {
  Opening: 'Abertura',
  SalePayment: 'Venda',
  CashIn: 'Entrada',
  CashOut: 'Saída',
  SaleRefund: 'Estorno de venda',
}
