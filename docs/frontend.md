# Frontend

StoreControl.Web is a separate React, TypeScript and Vite application. React Router owns nested routes, Axios owns HTTP concerns, and React Hook Form is used for login and administration forms.

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

Vite proxies /api and /health to localhost:5090. Docker Nginx proxies the same paths to the API container, so cookies remain same-origin.
