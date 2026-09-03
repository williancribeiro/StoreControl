import type { PagedResult } from '../registrations/registrationTypes'

export type TopSellingProduct = {
  productId: number
  productCode: string
  productName: string
  quantitySold: number
  grossRevenue: number
  saleCount: number
}

export type LowStockItem = {
  storeId: number
  storeCode: string
  storeName: string
  productId: number
  productCode: string
  productName: string
  quantityOnHand: number
  minimumQuantity: number
}

export type SalesEvolutionPoint = {
  date: string
  saleCount: number
  revenue: number
}

export type DashboardSummary = {
  referenceDate: string
  storeId: number | null
  salesCount: number
  revenue: number
  averageTicket: number
  lowStockCount: number
  topSellingProducts: TopSellingProduct[]
  lowStockItems: LowStockItem[]
  salesEvolution: SalesEvolutionPoint[]
}

export type ReportPeriodQuery = {
  fromDate: string
  toDate: string
  storeId?: number
}

export type SalesByStore = {
  storeId: number
  storeCode: string
  storeName: string
  saleCount: number
  revenue: number
  averageTicket: number
}

export type SalesReport = {
  fromDate: string
  toDate: string
  storeId: number | null
  saleCount: number
  grossRevenue: number
  discountAmount: number
  revenue: number
  averageTicket: number
  dailyEvolution: SalesEvolutionPoint[]
  byStore: SalesByStore[]
  topSellingProducts: TopSellingProduct[]
}

export type InventoryReportRow = {
  storeId: number
  storeCode: string
  storeName: string
  productId: number
  productCode: string
  productName: string
  categoryName: string
  quantityOnHand: number
  minimumQuantity: number
  isBelowMinimum: boolean
}

export type InventoryReportQuery = {
  page: number
  pageSize: number
  search?: string
  storeId?: number
  belowMinimum?: boolean
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type MovementTypeSummary = {
  type: string
  movementCount: number
  entryQuantity: number
  exitQuantity: number
}

export type MovementStoreSummary = {
  storeId: number
  storeCode: string
  storeName: string
  movementCount: number
  entryQuantity: number
  exitQuantity: number
}

export type MovementReport = {
  fromDate: string
  toDate: string
  storeId: number | null
  movementCount: number
  entryQuantity: number
  exitQuantity: number
  byType: MovementTypeSummary[]
  byStore: MovementStoreSummary[]
}

export type PurchaseStatusSummary = {
  status: string
  orderCount: number
  orderedAmount: number
}

export type PurchaseStoreSummary = {
  storeId: number
  storeCode: string
  storeName: string
  orderCount: number
  orderedAmount: number
  receivedAmount: number
}

export type PurchaseReport = {
  fromDate: string
  toDate: string
  storeId: number | null
  orderCount: number
  orderedAmount: number
  receivedAmount: number
  byStatus: PurchaseStatusSummary[]
  byStore: PurchaseStoreSummary[]
}

export type PagedInventoryReport = PagedResult<InventoryReportRow>
