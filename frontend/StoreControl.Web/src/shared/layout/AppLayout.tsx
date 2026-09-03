import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../features/auth/useAuth'
import { permissions } from '../../features/auth/authTypes'

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
          {hasPermission(permissions.inventoryRead) && (
            <>
              <span className="nav-section-label">Estoque</span>
              <NavLink className="nav-item" to="/inventory/positions">Posição atual</NavLink>
              <NavLink className="nav-item" to="/inventory/movements">Movimentações</NavLink>
              <NavLink className="nav-item" to="/inventory/transfers">Transferências</NavLink>
              {hasPermission(permissions.inventoryManage) && (
                <NavLink className="nav-item" to="/inventory/adjustments">Ajustes</NavLink>
              )}
            </>
          )}
          {hasPermission(permissions.purchasesRead) && (
            <>
              <span className="nav-section-label">Compras</span>
              {hasPermission(permissions.purchasesManage) && (
                <NavLink className="nav-item" to="/purchases/new">Nova compra</NavLink>
              )}
              <NavLink className="nav-item" to="/purchases">Histórico</NavLink>
            </>
          )}
          {hasPermission(permissions.salesRead) && (
            <>
              <span className="nav-section-label">Vendas</span>
              {hasPermission(permissions.salesManage) && (
                <NavLink className="nav-item" to="/sales/new">Nova venda</NavLink>
              )}
              <NavLink className="nav-item" to="/sales">Histórico</NavLink>
            </>
          )}
          {hasPermission(permissions.cashRead) && (
            <>
              <span className="nav-section-label">Caixa</span>
              {hasPermission(permissions.cashManage) && (
                <NavLink className="nav-item" to="/cash/current">Operação atual</NavLink>
              )}
              <NavLink className="nav-item" to="/cash/history">Histórico</NavLink>
            </>
          )}
          {hasPermission(permissions.reportsRead) && (
            <NavLink className="nav-item" to="/reports">Relatórios</NavLink>
          )}
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
  if (pathname.includes('/inventory/positions')) return 'Posição de estoque'
  if (pathname.includes('/inventory/movements')) return 'Histórico de estoque'
  if (pathname.includes('/inventory/adjustments')) return 'Ajustes de estoque'
  if (pathname === '/inventory/transfers/new') return 'Nova transferência'
  if (/^\/inventory\/transfers\/\d+$/.test(pathname)) return 'Detalhes da transferência'
  if (pathname === '/inventory/transfers') return 'Transferências entre lojas'
  if (pathname === '/purchases/new') return 'Nova compra'
  if (/^\/purchases\/\d+$/.test(pathname)) return 'Detalhes da compra'
  if (pathname === '/purchases') return 'Histórico de compras'
  if (pathname === '/sales/new') return 'Nova venda'
  if (/^\/sales\/\d+$/.test(pathname)) return 'Detalhes da venda'
  if (pathname === '/sales') return 'Histórico de vendas'
  if (pathname === '/cash/current') return 'Operação de caixa'
  if (/^\/cash\/sessions\/\d+$/.test(pathname)) return 'Detalhes do caixa'
  if (pathname === '/cash/history') return 'Histórico de caixas'
  if (pathname === '/reports') return 'Relatórios gerenciais'
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
