import { createBrowserRouter } from 'react-router-dom'
import { AccessDeniedPage } from '../features/auth/AccessDeniedPage'
import { DashboardPage } from '../features/auth/DashboardPage'
import { LoginPage } from '../features/auth/LoginPage'
import { ProfilePage } from '../features/auth/ProfilePage'
import { PermissionRoute, ProtectedRoute } from '../features/auth/ProtectedRoute'
import { permissions } from '../features/auth/authTypes'
import { RolesPage } from '../features/administration/RolesPage'
import { StoresPage } from '../features/administration/StoresPage'
import { UsersPage } from '../features/administration/UsersPage'
import { CategoriesPage } from '../features/registrations/CategoriesPage'
import { CustomersPage } from '../features/registrations/CustomersPage'
import { ProductsPage } from '../features/registrations/ProductsPage'
import { SuppliersPage } from '../features/registrations/SuppliersPage'
import { AppLayout } from '../shared/layout/AppLayout'
import { SmokePage } from '../features/system/SmokePage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        path: '/',
        element: <AppLayout />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: 'profile', element: <ProfilePage /> },
          { path: 'access-denied', element: <AccessDeniedPage /> },
          { path: 'system/smoke', element: <SmokePage /> },
          {
            element: <PermissionRoute permission={permissions.categoriesRead} />,
            children: [{ path: 'registrations/categories', element: <CategoriesPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.productsRead} />,
            children: [{ path: 'registrations/products', element: <ProductsPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.customersRead} />,
            children: [{ path: 'registrations/customers', element: <CustomersPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.suppliersRead} />,
            children: [{ path: 'registrations/suppliers', element: <SuppliersPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.usersRead} />,
            children: [{ path: 'administration/users', element: <UsersPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.rolesRead} />,
            children: [{ path: 'administration/roles', element: <RolesPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.storesRead} />,
            children: [{ path: 'administration/stores', element: <StoresPage /> }],
          },
        ],
      },
    ],
  },
])
