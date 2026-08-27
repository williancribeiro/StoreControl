import { httpClient } from '../../shared/api/httpClient'
import type {
  PagedUsers,
  PermissionSummary,
  RoleSummary,
  StoreSummary,
  UserAssignmentInput,
  UserSummary,
} from './adminTypes'

export async function listStores(signal?: AbortSignal) {
  const response = await httpClient.get<StoreSummary[]>('/api/stores', { signal })
  return response.data
}

export async function createStore(input: { code: string; name: string }) {
  const response = await httpClient.post<StoreSummary>('/api/stores', input)
  return response.data
}

export async function updateStore(
  storeId: number,
  input: { code: string; name: string; isActive: boolean; rowVersion: string },
) {
  const response = await httpClient.put<StoreSummary>(`/api/stores/${storeId}`, input)
  return response.data
}

export async function listRoles(signal?: AbortSignal) {
  const response = await httpClient.get<RoleSummary[]>('/api/roles', { signal })
  return response.data
}

export async function listPermissions(signal?: AbortSignal) {
  const response = await httpClient.get<PermissionSummary[]>('/api/roles/permissions', { signal })
  return response.data
}

export async function updateRolePermissions(roleId: string, permissions: string[]) {
  const response = await httpClient.put<RoleSummary>(`/api/roles/${roleId}/permissions`, {
    permissions,
  })
  return response.data
}

export async function listUsers(search = '', signal?: AbortSignal) {
  const response = await httpClient.get<PagedUsers>('/api/users', {
    params: { page: 1, pageSize: 100, search: search || undefined },
    signal,
  })
  return response.data
}

export async function createUser(input: {
  fullName: string
  email: string
  password: string
  isAdministrator: boolean
  storeAssignments: UserAssignmentInput[]
}) {
  const response = await httpClient.post<UserSummary>('/api/users', input)
  return response.data
}

export async function updateUser(
  userId: string,
  input: {
    fullName: string
    isActive: boolean
    isAdministrator: boolean
    storeAssignments: UserAssignmentInput[]
  },
) {
  const response = await httpClient.put<UserSummary>(`/api/users/${userId}`, input)
  return response.data
}

export async function resetUserPassword(userId: string, newPassword: string) {
  await httpClient.post(`/api/users/${userId}/reset-password`, { newPassword })
}
