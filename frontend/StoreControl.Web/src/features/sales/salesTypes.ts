import type { PagedResult } from '../registrations/registrationTypes'

export type PaymentMethod = 'Cash' | 'DebitCard' | 'CreditCard' | 'Pix' | 'Voucher' | 'Other'

export type SaleSummary = {
  id: number
  number: string
  operationId: string
  storeId: number
  storeCode: string
  storeName: string
  customerId: number | null
  customerCode: string | null
  customerName: string | null
  createdByUserId: string
  createdByUserName: string
  cashSessionId: number
  status: 'Finalized' | 'Cancelled'
  itemCount: number
  subtotal: number
  discountAmount: number
  totalAmount: number
  finalizedAtUtc: string
}

export type SaleDetails = SaleSummary & {
  cashRegisterCode: string
  createdAtUtc: string
  cancellationOperationId: string | null
  cancellationCashSessionId: number | null
  cancellationCashRegisterCode: string | null
  cancelledByUserId: string | null
  cancelledByUserName: string | null
  cancelledAtUtc: string | null
  cancellationReason: string | null
  rowVersion: string
  items: Array<{
    id: number
    productId: number
    productCode: string
    productName: string
    quantity: number
    unitPrice: number
    lineSubtotal: number
  }>
  payments: Array<{
    id: number
    method: PaymentMethod
    amount: number
    reference: string | null
  }>
  statusHistory: Array<{
    id: number
    status: 'Finalized' | 'Cancelled'
    changedByUserId: string
    changedByUserName: string
    changedAtUtc: string
    description: string
  }>
}

export type SaleQuery = {
  page: number
  pageSize: number
  search?: string
  storeId?: number
  customerId?: number
  paymentMethod?: PaymentMethod
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type PagedSales = PagedResult<SaleSummary>

export type FinalizeSaleInput = {
  storeId: number
  cashSessionId: number
  customerId?: number
  discountAmount: number
  items: Array<{ productId: number; quantity: number }>
  payments: Array<{ method: PaymentMethod; amount: number; reference?: string }>
}

export type FinalizeSaleResult = {
  sale: SaleDetails
  wasAlreadyProcessed: boolean
}

export type CancelSaleInput = {
  cashSessionId: number
  rowVersion: string
  reason: string
}

export type CancelSaleResult = {
  sale: SaleDetails
  wasAlreadyProcessed: boolean
}

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  Cash: 'Dinheiro',
  DebitCard: 'Cartão de débito',
  CreditCard: 'Cartão de crédito',
  Pix: 'Pix',
  Voucher: 'Voucher',
  Other: 'Outro',
}

export const paymentMethods = Object.keys(paymentMethodLabels) as PaymentMethod[]
