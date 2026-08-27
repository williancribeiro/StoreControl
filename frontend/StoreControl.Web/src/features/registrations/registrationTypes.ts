export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type CategorySummary = {
  id: number
  code: string
  name: string
  description: string | null
  isActive: boolean
  rowVersion: string
}

export type ProductSummary = {
  id: number
  code: string
  barcode: string | null
  name: string
  description: string | null
  categoryId: number
  categoryCode: string
  categoryName: string
  costPrice: number
  salePrice: number
  isActive: boolean
  rowVersion: string
}

export type PartySummary = {
  id: number
  code: string
  name: string
  document: string | null
  email: string | null
  phone: string | null
  isActive: boolean
  rowVersion: string
}

export type ListQuery = {
  page: number
  pageSize: number
  search?: string
  isActive?: boolean
  sortBy: string
  sortDirection: 'asc' | 'desc'
}

export type ProductListQuery = ListQuery & {
  categoryId?: number
}
