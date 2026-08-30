import { useCallback, useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { listPurchases } from './purchaseService'
import {
  purchaseStatusLabels,
  purchaseStatuses,
  type PagedPurchases,
  type PurchaseQuery,
  type PurchaseStatus,
} from './purchaseTypes'

const initialQuery: PurchaseQuery = {
  page: 1,
  pageSize: 20,
  sortBy: 'createdAt',
  sortDirection: 'desc',
}

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function PurchasesPage() {
  const { user, hasPermission } = useAuth()
  const [result, setResult] = useState<PagedPurchases | null>(null)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [storeFilter, setStoreFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (activeQuery: PurchaseQuery, signal?: AbortSignal) => {
    setResult(await listPurchases(activeQuery, signal))
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
  }, [user])

  function applyFilters() {
    setQuery((current) => ({
      ...current,
      page: 1,
      search: search.trim() || undefined,
      storeId: storeFilter === 'all' ? undefined : Number(storeFilter),
      status: statusFilter === 'all' ? undefined : statusFilter as PurchaseStatus,
    }))
  }

  return (
    <section className="panel table-panel purchases-wide">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Pedidos, recebimentos e histórico</span>
          <h2>Compras</h2>
        </div>
        {hasPermission(permissions.purchasesManage) && (
          <NavLink className="button button-primary" to="/purchases/new">Nova compra</NavLink>
        )}
      </div>

      <div className="filter-grid purchase-filters">
        <label>
          Busca
          <input
            value={search}
            placeholder="Número, fornecedor, loja ou produto"
            onChange={(event) => setSearch(event.target.value)}
            onKeyDown={(event) => event.key === 'Enter' && applyFilters()}
          />
        </label>
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
          Status
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="all">Todos</option>
            {purchaseStatuses.map((status) => (
              <option value={status} key={status}>{purchaseStatusLabels[status]}</option>
            ))}
          </select>
        </label>
        <label>
          Ordenar
          <select
            value={`${query.sortBy}:${query.sortDirection}`}
            onChange={(event) => {
              const [sortBy, sortDirection] = event.target.value.split(':')
              setQuery((current) => ({
                ...current,
                page: 1,
                sortBy,
                sortDirection: sortDirection as 'asc' | 'desc',
              }))
            }}
          >
            <option value="createdAt:desc">Mais recentes</option>
            <option value="createdAt:asc">Mais antigas</option>
            <option value="expectedDate:asc">Previsão mais próxima</option>
            <option value="supplier:asc">Fornecedor A–Z</option>
            <option value="total:desc">Maior valor</option>
            <option value="status:asc">Status</option>
          </select>
        </label>
        <button className="button button-secondary filter-action" type="button" onClick={applyFilters}>
          Aplicar
        </button>
      </div>

      {error && <div className="form-error page-message">{error}</div>}
      <div className="table-scroll">
        <table>
          <thead><tr><th>Pedido</th><th>Fornecedor</th><th>Loja</th><th>Status</th><th>Itens</th><th>Total</th><th>Criação</th><th /></tr></thead>
          <tbody>
            {result?.items.map((purchase) => (
              <tr key={purchase.id}>
                <td><strong>{purchase.number}</strong><small className="table-subtitle">Previsão {purchase.expectedDate ? new Date(`${purchase.expectedDate}T00:00:00`).toLocaleDateString('pt-BR') : 'não informada'}</small></td>
                <td>{purchase.supplierName}<small className="table-subtitle">{purchase.supplierCode}</small></td>
                <td>{purchase.storeCode}<small className="table-subtitle">{purchase.storeName}</small></td>
                <td><span className={`status-pill purchase-${purchase.status.toLowerCase()}`}>{purchaseStatusLabels[purchase.status]}</span></td>
                <td>{purchase.itemCount}</td>
                <td>{money.format(purchase.totalAmount)}</td>
                <td>{dateTime.format(new Date(purchase.createdAtUtc))}</td>
                <td className="table-actions"><NavLink className="link-button" to={`/purchases/${purchase.id}`}>Detalhes</NavLink></td>
              </tr>
            ))}
            {result?.items.length === 0 && <tr><td colSpan={8}>Nenhum pedido encontrado para os filtros.</td></tr>}
          </tbody>
        </table>
      </div>
      {result && (
        <Pagination
          page={result.page}
          totalPages={result.totalPages}
          totalCount={result.totalCount}
          onPageChange={(page) => setQuery((current) => ({ ...current, page }))}
        />
      )}
    </section>
  )
}
