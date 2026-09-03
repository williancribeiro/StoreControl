import { httpClient } from '../../shared/api/httpClient'
import type {
  DashboardSummary,
  InventoryReportQuery,
  MovementReport,
  PagedInventoryReport,
  PurchaseReport,
  ReportPeriodQuery,
  SalesReport,
} from './reportingTypes'

export async function getDashboard(storeId?: number, date?: string, signal?: AbortSignal) {
  const response = await httpClient.get<DashboardSummary>('/api/dashboard', {
    params: { storeId, date },
    signal,
  })
  return response.data
}

export async function getSalesReport(query: ReportPeriodQuery, signal?: AbortSignal) {
  const response = await httpClient.get<SalesReport>('/api/reports/sales', {
    params: query,
    signal,
  })
  return response.data
}

export async function getInventoryReport(query: InventoryReportQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedInventoryReport>('/api/reports/inventory', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function getMovementReport(query: ReportPeriodQuery, signal?: AbortSignal) {
  const response = await httpClient.get<MovementReport>('/api/reports/movements', {
    params: query,
    signal,
  })
  return response.data
}

export async function getPurchaseReport(query: ReportPeriodQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PurchaseReport>('/api/reports/purchases', {
    params: query,
    signal,
  })
  return response.data
}

function cleanQuery(query: InventoryReportQuery) {
  return Object.fromEntries(
    Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
  )
}
