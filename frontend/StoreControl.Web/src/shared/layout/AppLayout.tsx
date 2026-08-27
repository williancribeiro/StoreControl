import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../features/auth/useAuth'
import { permissions } from '../../features/auth/authTypes'

const plannedModules = ['Vendas', 'Compras', 'Estoque', 'Caixa', 'Relatórios']

export function AppLayout() {
  const { user, hasPermission, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const title = getPageTitle(location.pathname)

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">SC</span>
          <div>
            <strong>StoreControl</strong>
            <small>Laboratório empresarial</small>
          </div>
        </div>

        <nav aria-label="Navegação principal">
          <NavLink className="nav-item" to="/" end>
            Dashboard
          </NavLink>
          {(hasPermission(permissions.categoriesRead) ||
            hasPermission(permissions.productsRead) ||
            hasPermission(permissions.customersRead) ||
            hasPermission(permissions.suppliersRead)) && (
            <span className="nav-section-label">Cadastros</span>
          )}
          {hasPermission(permissions.categoriesRead) && (
            <NavLink className="nav-item" to="/registrations/categories">Categorias</NavLink>
          )}
          {hasPermission(permissions.productsRead) && (
            <NavLink className="nav-item" to="/registrations/products">Produtos</NavLink>
          )}
          {hasPermission(permissions.customersRead) && (
            <NavLink className="nav-item" to="/registrations/customers">Clientes</NavLink>
          )}
          {hasPermission(permissions.suppliersRead) && (
            <NavLink className="nav-item" to="/registrations/suppliers">Fornecedores</NavLink>
          )}
          {plannedModules.map((module) => (
            <span className="nav-item nav-item-disabled" key={module}>
              {module}
              <small>planejado</small>
            </span>
          ))}
          {(hasPermission(permissions.usersRead) ||
            hasPermission(permissions.rolesRead) ||
            hasPermission(permissions.storesRead)) && (
            <span className="nav-section-label">Administração</span>
          )}
          {hasPermission(permissions.usersRead) && (
            <NavLink className="nav-item" to="/administration/users">
              Usuários
            </NavLink>
          )}
          {hasPermission(permissions.rolesRead) && (
            <NavLink className="nav-item" to="/administration/roles">
              Perfis
            </NavLink>
          )}
          {hasPermission(permissions.storesRead) && (
            <NavLink className="nav-item" to="/administration/stores">
              Lojas
            </NavLink>
          )}
          <span className="nav-section-label">Sistema</span>
          <NavLink className="nav-item" to="/system/smoke">
            Smoke test
          </NavLink>
        </nav>

        <div className="sidebar-footer">
          <div>
            <span className="environment-dot" aria-hidden="true" />
            Ambiente local
          </div>
          <small>{user?.email}</small>
        </div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div>
            <span className="eyebrow">StoreControl · ambiente local</span>
            <h1>{title}</h1>
          </div>
          <div className="account-actions">
            <NavLink className="account-link" to="/profile">
              <span>{getInitials(user?.fullName ?? '')}</span>
              <strong>{user?.fullName}</strong>
            </NavLink>
            <button
              className="link-button"
              type="button"
              onClick={async () => {
                await logout()
                navigate('/login', { replace: true })
              }}
            >
              Sair
            </button>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  )
}

function getPageTitle(pathname: string) {
  if (pathname === '/profile') return 'Minha identidade'
  if (pathname.includes('/categories')) return 'Categorias de produtos'
  if (pathname.includes('/products')) return 'Catálogo de produtos'
  if (pathname.includes('/customers')) return 'Clientes'
  if (pathname.includes('/suppliers')) return 'Fornecedores'
  if (pathname.includes('/users')) return 'Usuários'
  if (pathname.includes('/roles')) return 'Perfis e permissões'
  if (pathname.includes('/stores')) return 'Lojas e filiais'
  if (pathname.includes('/smoke')) return 'Diagnóstico da fundação'
  if (pathname.includes('/access-denied')) return 'Acesso negado'
  return 'Dashboard'
}

function getInitials(fullName: string) {
  return fullName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}
