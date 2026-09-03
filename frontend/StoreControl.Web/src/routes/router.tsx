import { createBrowserRouter } from 'react-router-dom'
import { AccessDeniedPage } from '../features/auth/AccessDeniedPage'
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
import { StockAdjustmentsPage } from '../features/inventory/StockAdjustmentsPage'
import { StockMovementsPage } from '../features/inventory/StockMovementsPage'
import { StockPositionsPage } from '../features/inventory/StockPositionsPage'
import { NewPurchasePage } from '../features/purchasing/NewPurchasePage'
import { PurchaseDetailsPage } from '../features/purchasing/PurchaseDetailsPage'
import { PurchasesPage } from '../features/purchasing/PurchasesPage'
import { CashHistoryPage } from '../features/cash/CashHistoryPage'
import { CashOperationPage } from '../features/cash/CashOperationPage'
import { CashSessionDetailsPage } from '../features/cash/CashSessionDetailsPage'
import { NewSalePage } from '../features/sales/NewSalePage'
import { SaleDetailsPage } from '../features/sales/SaleDetailsPage'
import { SalesPage } from '../features/sales/SalesPage'
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
          {
            index: true,
            lazy: async () => ({
              Component: (await import('../features/auth/DashboardPage')).DashboardPage,
            }),
          },
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
            element: <PermissionRoute permission={permissions.inventoryRead} />,
            children: [
              { path: 'inventory/positions', element: <StockPositionsPage /> },
              { path: 'inventory/movements', element: <StockMovementsPage /> },
              {
                path: 'inventory/transfers',
                lazy: async () => ({
                  Component: (await import('../features/inventory/StockTransfersPage')).StockTransfersPage,
                }),
              },
              {
                path: 'inventory/transfers/:stockTransferId',
                lazy: async () => ({
                  Component: (await import('../features/inventory/StockTransferDetailsPage')).StockTransferDetailsPage,
                }),
              },
            ],
          },
          {
            element: <PermissionRoute permission={permissions.inventoryManage} />,
            children: [
              { path: 'inventory/adjustments', element: <StockAdjustmentsPage /> },
              {
                path: 'inventory/transfers/new',
                lazy: async () => ({
                  Component: (await import('../features/inventory/NewStockTransferPage')).NewStockTransferPage,
                }),
              },
            ],
          },
          {
            element: <PermissionRoute permission={permissions.purchasesRead} />,
            children: [
              { path: 'purchases', element: <PurchasesPage /> },
              { path: 'purchases/:purchaseId', element: <PurchaseDetailsPage /> },
            ],
          },
          {
            element: <PermissionRoute permission={permissions.purchasesManage} />,
            children: [{ path: 'purchases/new', element: <NewPurchasePage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.salesRead} />,
            children: [
              { path: 'sales', element: <SalesPage /> },
              { path: 'sales/:saleId', element: <SaleDetailsPage /> },
            ],
          },
          {
            element: <PermissionRoute permission={permissions.salesManage} />,
            children: [{ path: 'sales/new', element: <NewSalePage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.cashRead} />,
            children: [
              { path: 'cash/history', element: <CashHistoryPage /> },
              { path: 'cash/sessions/:cashSessionId', element: <CashSessionDetailsPage /> },
            ],
          },
          {
            element: <PermissionRoute permission={permissions.cashManage} />,
            children: [{ path: 'cash/current', element: <CashOperationPage /> }],
          },
          {
            element: <PermissionRoute permission={permissions.reportsRead} />,
            children: [
              {
                path: 'reports',
                lazy: async () => ({
                  Component: (await import('../features/reporting/ReportsPage')).ReportsPage,
                }),
              },
            ],
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
