import { httpClient } from '../../shared/api/httpClient'
import type {
  CategorySummary,
  ListQuery,
  PagedResult,
  PartySummary,
  ProductListQuery,
  ProductSummary,
} from './registrationTypes'

function cleanQuery(query: ListQuery | ProductListQuery) {
  return {
    ...query,
    search: query.search || undefined,
    isActive: query.isActive,
  }
}

export async function listCategories(query: ListQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedResult<CategorySummary>>('/api/categories', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function createCategory(input: {
  code: string
  name: string
  description?: string
}) {
  const response = await httpClient.post<CategorySummary>('/api/categories', input)
  return response.data
}

export async function updateCategory(
  categoryId: number,
  input: {
    code: string
    name: string
    description?: string
    isActive: boolean
    rowVersion: string
  },
) {
  const response = await httpClient.put<CategorySummary>(`/api/categories/${categoryId}`, input)
  return response.data
}

export async function listProducts(query: ProductListQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedResult<ProductSummary>>('/api/products', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function createProduct(input: {
  code: string
  barcode?: string
  name: string
  description?: string
  categoryId: number
  costPrice: number
  salePrice: number
}) {
  const response = await httpClient.post<ProductSummary>('/api/products', input)
  return response.data
}

export async function updateProduct(
  productId: number,
  input: {
    code: string
    barcode?: string
    name: string
    description?: string
    categoryId: number
    costPrice: number
    salePrice: number
    isActive: boolean
    rowVersion: string
  },
) {
  const response = await httpClient.put<ProductSummary>(`/api/products/${productId}`, input)
  return response.data
}

export async function listCustomers(query: ListQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedResult<PartySummary>>('/api/customers', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function createCustomer(input: PartyInput) {
  const response = await httpClient.post<PartySummary>('/api/customers', input)
  return response.data
}

export async function updateCustomer(partyId: number, input: PartyUpdateInput) {
  const response = await httpClient.put<PartySummary>(`/api/customers/${partyId}`, input)
  return response.data
}

export async function listSuppliers(query: ListQuery, signal?: AbortSignal) {
  const response = await httpClient.get<PagedResult<PartySummary>>('/api/suppliers', {
    params: cleanQuery(query),
    signal,
  })
  return response.data
}

export async function createSupplier(input: PartyInput) {
  const response = await httpClient.post<PartySummary>('/api/suppliers', input)
  return response.data
}

export async function updateSupplier(partyId: number, input: PartyUpdateInput) {
  const response = await httpClient.put<PartySummary>(`/api/suppliers/${partyId}`, input)
  return response.data
}

export type PartyInput = {
  code: string
  name: string
  document?: string
  email?: string
  phone?: string
}

export type PartyUpdateInput = PartyInput & {
  isActive: boolean
  rowVersion: string
}
