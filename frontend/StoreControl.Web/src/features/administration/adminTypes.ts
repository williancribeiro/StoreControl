export type StoreSummary = {
  id: number
  code: string
  name: string
  isActive: boolean
  rowVersion: string
}

export type PermissionSummary = {
  code: string
  description: string
}

export type RoleSummary = {
  id: string
  name: string
  isSystemRole: boolean
  permissions: string[]
}

export type UserStoreAssignment = {
  storeId: number
  storeCode: string
  storeName: string
  roleId: string
  roleName: string
}

export type UserSummary = {
  id: string
  fullName: string
  email: string
  isActive: boolean
  isAdministrator: boolean
  createdAtUtc: string
  lastLoginAtUtc: string | null
  storeAssignments: UserStoreAssignment[]
}

export type PagedUsers = {
  items: UserSummary[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type UserAssignmentInput = {
  storeId: number
  roleId: string
}
