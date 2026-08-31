import { useCallback, useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { listSales } from './salesService'
import { paymentMethodLabels, paymentMethods, type PagedSales, type PaymentMethod, type SaleQuery } from './salesTypes'

const initialQuery: SaleQuery = { page: 1, pageSize: 20, sortBy: 'finalizedAt', sortDirection: 'desc' }
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function SalesPage() {
  const { user, hasPermission } = useAuth()
  const [result, setResult] = useState<PagedSales | null>(null)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [storeFilter, setStoreFilter] = useState('all')
  const [paymentFilter, setPaymentFilter] = useState('all')
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (activeQuery: SaleQuery, signal?: AbortSignal) => {
    setResult(await listSales(activeQuery, signal))
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load(query, controller.signal).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [load, query])

  useEffect(() => {
    const assigned = user?.stores.map((store) => ({
      id: store.storeId, code: store.storeCode, name: store.storeName, isActive: true, rowVersion: '',
    })) ?? []
    if (assigned.length > 0) {
      // oxlint-disable-next-line react/set-state-in-effect
      setStores(assigned)
      return
    }
    const controller = new AbortController()
    void listStores(controller.signal).then((items) => setStores(items.filter((store) => store.isActive)))
      .catch((requestError) => { if (!controller.signal.aborted) setError(getProblemMessage(requestError)) })
    return () => controller.abort()
  }, [user])

  function applyFilters() {
    setQuery((current) => ({
      ...current,
      page: 1,
      search: search.trim() || undefined,
      storeId: storeFilter === 'all' ? undefined : Number(storeFilter),
      paymentMethod: paymentFilter === 'all' ? undefined : paymentFilter as PaymentMethod,
    }))
  }

  return (
    <section className="panel table-panel purchases-wide">
      <div className="section-heading">
        <div><span className="eyebrow">Documentos comerciais finalizados</span><h2>Histórico de vendas</h2></div>
        {hasPermission(permissions.salesManage) && <NavLink className="button button-primary" to="/sales/new">Nova venda</NavLink>}
      </div>
      <div className="filter-grid sales-filters">
        <label>Busca<input value={search} placeholder="Número, cliente, loja ou produto" onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => event.key === 'Enter' && applyFilters()} /></label>
        <label>Loja<select value={storeFilter} onChange={(event) => setStoreFilter(event.target.value)}><option value="all">Todas acessíveis</option>{stores.map((store) => <option value={store.id} key={store.id}>{store.code} · {store.name}</option>)}</select></label>
        <label>Pagamento<select value={paymentFilter} onChange={(event) => setPaymentFilter(event.target.value)}><option value="all">Todos</option>{paymentMethods.map((method) => <option value={method} key={method}>{paymentMethodLabels[method]}</option>)}</select></label>
        <label>Ordenar<select value={`${query.sortBy}:${query.sortDirection}`} onChange={(event) => {
          const [sortBy, sortDirection] = event.target.value.split(':')
          setQuery((current) => ({ ...current, page: 1, sortBy, sortDirection: sortDirection as 'asc' | 'desc' }))
        }}><option value="finalizedAt:desc">Mais recentes</option><option value="finalizedAt:asc">Mais antigas</option><option value="total:desc">Maior valor</option><option value="customer:asc">Cliente A–Z</option><option value="store:asc">Loja</option></select></label>
        <button className="button button-secondary filter-action" type="button" onClick={applyFilters}>Aplicar</button>
      </div>
      {error && <div className="form-error page-message">{error}</div>}
      <div className="table-scroll"><table><thead><tr><th>Venda</th><th>Cliente</th><th>Loja</th><th>Operador</th><th>Itens</th><th>Desconto</th><th>Total</th><th>Finalização</th><th /></tr></thead><tbody>
        {result?.items.map((sale) => <tr key={sale.id}>
          <td><strong>{sale.number}</strong><small className="table-subtitle">{sale.status === 'Cancelled' ? 'Cancelada' : 'Finalizada'}</small></td>
          <td>{sale.customerName ?? 'Consumidor não identificado'}{sale.customerCode && <small className="table-subtitle">{sale.customerCode}</small>}</td>
          <td>{sale.storeCode}<small className="table-subtitle">{sale.storeName}</small></td>
          <td>{sale.createdByUserName}</td><td>{sale.itemCount}</td><td>{money.format(sale.discountAmount)}</td><td><strong>{money.format(sale.totalAmount)}</strong></td><td>{dateTime.format(new Date(sale.finalizedAtUtc))}</td>
          <td className="table-actions"><NavLink className="link-button" to={`/sales/${sale.id}`}>Detalhes</NavLink></td>
        </tr>)}
        {result?.items.length === 0 && <tr><td colSpan={9}>Nenhuma venda encontrada.</td></tr>}
      </tbody></table></div>
      {result && <Pagination page={result.page} totalPages={result.totalPages} totalCount={result.totalCount} onPageChange={(page) => setQuery((current) => ({ ...current, page }))} />}
    </section>
  )
}
