# Frontend

StoreControl.Web is a separate React, TypeScript and Vite application. React Router owns nested routes, Axios owns HTTP concerns, and React Hook Form is used for authenticated write forms.

## Structure

    src/
    ├── features/
    │   ├── administration/
    │   │   ├── UsersPage
    │   │   ├── RolesPage
    │   │   └── StoresPage
    │   ├── auth/
    │   │   ├── AuthProvider
    │   │   ├── LoginPage
    │   │   ├── ProfilePage
    │   │   └── route guards
    │   ├── registrations/
    │   │   ├── CategoriesPage
    │   │   ├── ProductsPage
    │   │   ├── CustomersPage / SuppliersPage
    │   │   ├── shared party form and pagination
    │   │   └── registration service/types
    │   ├── inventory/
    │   │   ├── StockPositionsPage
    │   │   ├── StockMovementsPage
    │   │   ├── StockAdjustmentsPage
    │   │   ├── StockTransfersPage / NewStockTransferPage / StockTransferDetailsPage
    │   │   └── inventory and transfer services/types
    │   ├── purchasing/
    │   │   ├── NewPurchasePage
    │   │   ├── PurchasesPage
    │   │   ├── PurchaseDetailsPage
    │   │   └── purchase service/types
    │   ├── sales/
    │   │   ├── NewSalePage
    │   │   ├── SalesPage / SaleDetailsPage
    │   │   └── sales service/types
    │   ├── cash/
    │   │   ├── CashOperationPage
    │   │   ├── CashHistoryPage / CashSessionDetailsPage
    │   │   └── cash service/types
    │   └── system/SmokePage
    ├── routes/
    └── shared/
        ├── api/
        └── layout/

Simple pages remain direct. Session coordination and the multi-field user editor have dedicated services/types because they carry real complexity.

## Session

The access token lives only in module memory. AuthProvider restores a session through the HttpOnly refresh cookie. A shared Axios interceptor adds Authorization and performs one deduplicated refresh after 401. It retries the original request once.

No browser storage contains tokens. Full page reloads therefore require refresh, as intended.

## Routes

- /login;
- / dashboard;
- /profile;
- /administration/users;
- /administration/roles;
- /administration/stores;
- /registrations/categories;
- /registrations/products;
- /registrations/customers;
- /registrations/suppliers;
- /inventory/positions;
- /inventory/movements;
- /inventory/adjustments;
- /inventory/transfers;
- /inventory/transfers/new;
- /inventory/transfers/:stockTransferId;
- /purchases/new;
- /purchases;
- /purchases/:purchaseId;
- /sales/new;
- /sales;
- /sales/:saleId;
- /cash/current;
- /cash/history;
- /cash/sessions/:cashSessionId;
- /system/smoke;
- /access-denied.

ProtectedRoute handles authentication and PermissionRoute handles feature visibility. They are user-experience controls only; the backend policies remain authoritative.

## Screens

- login with validation and honest error state;
- current identity, effective roles, permissions and stores;
- users list/search, create/edit, activation, global administrator flag, per-store profiles and password reset;
- roles and permission mapping, with protected Administrator mapping;
- stores create/edit/activate/inactivate with rowversion;
- original real smoke test.
- category, product, customer and supplier tables with server pagination, filters, search and sort;
- create/edit forms with React Hook Form, permission-aware actions, active state and rowversion;
- product category binding and decimal price inputs.
- stock position by product/store with minimum and low-stock status;
- movement history with signed change, before/after, actor and correlation;
- positive/negative adjustment form that loads the current rowversion and reports 409 conflicts honestly.
- purchase creation with active store/supplier/product selection, historical unit costs and an item total preview;
- paginated purchase history with store/status/search/sort filters;
- purchase details with status history, receipt history, lifecycle actions and partial/full receipt;
- receipt retry reuses its client UUID until confirmed so an uncertain HTTP result does not duplicate stock.
- PDV-style new sale with store/register context, open-session feedback, product search, editable cart quantities, optional customer, discount, split payment methods, total reconciliation and transactional error feedback;
- sale history/detail with historical item prices and payment split;
- sale detail with cancellation reason, open refund-session selection, retained idempotency key and status audit;
- current cash operation with opening, live signed ledger, manual entry/exit and physical close;
- cash session history/detail with expected/declared amount and difference;
- sale finalization retains one UUID across uncertain retries, while the server remains authoritative for price, stock, money and authorization.
- transfer history with origin/destination/status filters, creation cart and detail lifecycle actions;
- transfer create/dispatch/receive/cancel operations retain one UUID each across uncertain retries and expose rowversion conflicts without automatic overwrite;

The three transfer routes are loaded lazily so the operational screens do not inflate the main application bundle.

Vite proxies /api and /health to localhost:5090. Docker Nginx proxies the same paths to the API container, so cookies remain same-origin.
