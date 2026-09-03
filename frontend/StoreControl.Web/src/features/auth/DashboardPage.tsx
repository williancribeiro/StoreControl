import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { getDashboard } from '../reporting/reportingService'
import type { DashboardSummary } from '../reporting/reportingTypes'
import { permissions } from './authTypes'
import { useAuth } from './useAuth'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const shortDate = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' })
const today = new Date().toISOString().slice(0, 10)

export function DashboardPage() {
  const { user, hasPermission } = useAuth()
  const canViewReports = hasPermission(permissions.reportsRead)
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [storeFilter, setStoreFilter] = useState('all')
  const [referenceDate, setReferenceDate] = useState(today)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!canViewReports) return
    const assignedStores = user?.stores.map((store) => ({
      id: store.storeId,
      code: store.storeCode,
      name: store.storeName,
      isActive: true,
      rowVersion: '',
    })) ?? []
    if (assignedStores.length > 0) {
      // oxlint-disable-next-line react/set-state-in-effect
      setStores(assignedStores)
      return
    }

    const controller = new AbortController()
    void listStores(controller.signal)
      .then((items) => setStores(items.filter((store) => store.isActive)))
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [canViewReports, user])

  useEffect(() => {
    if (!canViewReports) return
    const controller = new AbortController()
    void getDashboard(
      storeFilter === 'all' ? undefined : Number(storeFilter),
      referenceDate,
      controller.signal,
    )
      .then((result) => {
        setSummary(result)
        setError(null)
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [canViewReports, referenceDate, storeFilter])

  const maximumRevenue = useMemo(
    () => Math.max(1, ...(summary?.salesEvolution.map((point) => point.revenue) ?? [])),
    [summary],
  )

  if (!user) return null
  if (!canViewReports) return <IdentityOverview />

  return (
    <section className="business-dashboard">
      <div className="dashboard-toolbar panel">
        <div>
          <span className="eyebrow">Visão operacional consolidada</span>
          <h2>Indicadores do dia</h2>
        </div>
        <label>
          Loja
          <select value={storeFilter} onChange={(event) => setStoreFilter(event.target.value)}>
            <option value="all">Todas acessíveis</option>
            {stores.map((store) => (
              <option value={store.id} key={store.id}>{store.code} · {store.name}</option>
            ))}
          </select>
        </label>
        <label>
          Data de referência (UTC)
          <input
            type="date"
            value={referenceDate}
            onChange={(event) => setReferenceDate(event.target.value)}
          />
        </label>
      </div>

      {error && <div className="form-error page-message">{error}</div>}
      {!summary && !error && <article className="panel status-message">Carregando indicadores…</article>}
      {summary && (
        <>
          <div className="dashboard-kpis">
            <MetricCard label="Vendas" value={quantity.format(summary.salesCount)} detail="finalizadas no dia" />
            <MetricCard label="Faturamento" value={money.format(summary.revenue)} detail="vendas líquidas válidas" />
            <MetricCard label="Ticket médio" value={money.format(summary.averageTicket)} detail="faturamento ÷ vendas" />
            <MetricCard label="Estoque baixo" value={quantity.format(summary.lowStockCount)} detail="posições no mínimo ou abaixo" alert={summary.lowStockCount > 0} />
          </div>

          <div className="dashboard-report-grid">
            <article className="panel chart-panel">
              <div className="section-heading">
                <div><span className="eyebrow">Últimos sete dias</span><h2>Evolução de vendas</h2></div>
                <Link className="link-button" to="/reports">Abrir relatórios</Link>
              </div>
              <div className="sales-chart" role="img" aria-label="Faturamento diário dos últimos sete dias">
                {summary.salesEvolution.map((point) => (
                  <div className="sales-chart-column" key={point.date}>
                    <span>{money.format(point.revenue)}</span>
                    <div className="sales-chart-track">
                      <i style={{ height: `${Math.max(3, (point.revenue / maximumRevenue) * 100)}%` }} />
                    </div>
                    <strong>{shortDate.format(new Date(`${point.date}T00:00:00`))}</strong>
                    <small>{point.saleCount} venda(s)</small>
                  </div>
                ))}
              </div>
            </article>

            <article className="panel dashboard-list-panel">
              <span className="eyebrow">Por quantidade</span>
              <h2>Produtos mais vendidos</h2>
              <ol className="ranking-list">
                {summary.topSellingProducts.map((product) => (
                  <li key={product.productId}>
                    <span><strong>{product.productName}</strong><small>{product.productCode}</small></span>
                    <span><strong>{quantity.format(product.quantitySold)}</strong><small>{money.format(product.grossRevenue)}</small></span>
                  </li>
                ))}
                {summary.topSellingProducts.length === 0 && <li className="list-empty">Sem vendas na data.</li>}
              </ol>
            </article>

            <article className="panel dashboard-list-panel panel-wide">
              <div className="section-heading">
                <div><span className="eyebrow">Reposição</span><h2>Posições com estoque baixo</h2></div>
                <Link className="link-button" to="/inventory/positions">Ver estoque</Link>
              </div>
              <div className="table-scroll">
                <table>
                  <thead><tr><th>Produto</th><th>Loja</th><th>Disponível</th><th>Mínimo</th></tr></thead>
                  <tbody>
                    {summary.lowStockItems.map((item) => (
                      <tr key={`${item.storeId}:${item.productId}`}>
                        <td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td>
                        <td>{item.storeCode}<small className="table-subtitle">{item.storeName}</small></td>
                        <td className="quantity-negative">{quantity.format(item.quantityOnHand)}</td>
                        <td>{quantity.format(item.minimumQuantity)}</td>
                      </tr>
                    ))}
                    {summary.lowStockItems.length === 0 && <tr><td colSpan={4}>Nenhuma posição abaixo do mínimo.</td></tr>}
                  </tbody>
                </table>
              </div>
            </article>
          </div>
        </>
      )}
    </section>
  )
}

function MetricCard({
  label,
  value,
  detail,
  alert = false,
}: {
  label: string
  value: string
  detail: string
  alert?: boolean
}) {
  return (
    <article className={`panel metric-card dashboard-metric${alert ? ' dashboard-metric-alert' : ''}`}>
      <span className="metric-value">{value}</span>
      <strong>{label}</strong>
      <small>{detail}</small>
    </article>
  )
}

function IdentityOverview() {
  const { user } = useAuth()
  if (!user) return null
  return (
    <section className="dashboard-grid">
      <article className="panel welcome-panel">
        <span className="eyebrow">Sessão autenticada</span>
        <h2>Olá, {user.fullName}.</h2>
        <p>As áreas exibidas no menu refletem as permissões desta sessão. Indicadores empresariais exigem a permissão reports.read, verificada pela API.</p>
        <Link className="button button-secondary" to="/profile">Ver identidade e claims</Link>
      </article>
      <MetricCard label="perfis efetivos" value={`${user.roles.length}`} detail="globais e associados a lojas" />
      <MetricCard label="lojas atribuídas" value={`${user.stores.length}`} detail="o administrador possui escopo global" />
      <MetricCard label="policies autorizadas" value={`${user.permissions.length}`} detail="claims emitidas pela API" />
    </section>
  )
}
