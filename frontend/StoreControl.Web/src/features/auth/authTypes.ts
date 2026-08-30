export type UserStoreAccess = {
  storeId: number
  storeCode: string
  storeName: string
  role: string
}

export type CurrentUser = {
  id: string
  fullName: string
  email: string
  roles: string[]
  permissions: string[]
  stores: UserStoreAccess[]
}

export type AuthResponse = {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: CurrentUser
}

export const permissions = {
  storesRead: 'stores.read',
  storesManage: 'stores.manage',
  usersRead: 'users.read',
  usersManage: 'users.manage',
  rolesRead: 'roles.read',
  rolesManage: 'roles.manage',
  categoriesRead: 'categories.read',
  categoriesManage: 'categories.manage',
  productsRead: 'products.read',
  productsManage: 'products.manage',
  customersRead: 'customers.read',
  customersManage: 'customers.manage',
  suppliersRead: 'suppliers.read',
  suppliersManage: 'suppliers.manage',
  inventoryRead: 'inventory.read',
  inventoryManage: 'inventory.manage',
  purchasesRead: 'purchases.read',
  purchasesManage: 'purchases.manage',
  salesRead: 'sales.read',
  salesManage: 'sales.manage',
  cashRead: 'cash.read',
  cashManage: 'cash.manage',
} as const
