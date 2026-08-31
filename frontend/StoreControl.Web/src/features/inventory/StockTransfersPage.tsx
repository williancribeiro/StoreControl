import { useCallback, useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { listStockTransfers } from './transferService'
import {
  transferStatusLabels,
  transferStatuses,
  type PagedStockTransfers,
  type StockTransferQuery,
  type StockTransferStatus,
} from './transferTypes'

const initialQuery: StockTransferQuery = {
  page: 1,
  pageSize: 20,
  sortBy: 'createdAt',
  sortDirection: 'desc',
}

const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function StockTransfersPage() {
  const { user, hasPermission } = useAuth()
  const [result, setResult] = useState<PagedStockTransfers | null>(null)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [originStore, setOriginStore] = useState('all')
  const [destinationStore, setDestinationStore] = useState('all')
  const [status, setStatus] = useState('all')
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (activeQuery: StockTransferQuery, signal?: AbortSignal) => {
    setResult(await listStockTransfers(activeQuery, signal))
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
      id: store.storeId,
      code: store.storeCode,
      name: store.storeName,
      isActive: true,
      rowVersion: '',
    })) ?? []
    if (assigned.length > 0) {
      // oxlint-disable-next-line react/set-state-in-effect
      setStores(assigned)
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
      originStoreId: originStore === 'all' ? undefined : Number(originStore),
      destinationStoreId: destinationStore === 'all' ? undefined : Number(destinationStore),
      status: status === 'all' ? undefined : status as StockTransferStatus,
    }))
  }

  return (
    <section className="panel table-panel inventory-wide">
      <div className="section-heading">
        <div><span className="eyebrow">Saída, trânsito e entrada auditáveis</span><h2>Transferências</h2></div>
        {hasPermission(permissions.inventoryManage) && <NavLink className="button button-primary" to="/inventory/transfers/new">Nova transferência</NavLink>}
      </div>
      <div className="filter-grid transfer-filters">
        <label>Busca<input value={search} placeholder="Número, loja ou produto" onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => event.key === 'Enter' && applyFilters()} /></label>
        <label>Origem<select value={originStore} onChange={(event) => setOriginStore(event.target.value)}><option value="all">Todas acessíveis</option>{stores.map((store) => <option value={store.id} key={store.id}>{store.code} · {store.name}</option>)}</select></label>
        <label>Destino<select value={destinationStore} onChange={(event) => setDestinationStore(event.target.value)}><option value="all">Todos acessíveis</option>{stores.map((store) => <option value={store.id} key={store.id}>{store.code} · {store.name}</option>)}</select></label>
        <label>Status<select value={status} onChange={(event) => setStatus(event.target.value)}><option value="all">Todos</option>{transferStatuses.map((item) => <option value={item} key={item}>{transferStatusLabels[item]}</option>)}</select></label>
        <label>Ordenar<select value={`${query.sortBy}:${query.sortDirection}`} onChange={(event) => { const [sortBy, sortDirection] = event.target.value.split(':'); setQuery((current) => ({ ...current, page: 1, sortBy, sortDirection: sortDirection as 'asc' | 'desc' })) }}><option value="createdAt:desc">Mais recentes</option><option value="createdAt:asc">Mais antigas</option><option value="origin:asc">Origem A–Z</option><option value="destination:asc">Destino A–Z</option><option value="status:asc">Status</option></select></label>
        <button className="button button-secondary filter-action" type="button" onClick={applyFilters}>Aplicar</button>
      </div>
      {error && <div className="form-error page-message">{error}</div>}
      <div className="table-scroll"><table><thead><tr><th>Transferência</th><th>Origem</th><th>Destino</th><th>Status</th><th>Itens</th><th>Quantidade</th><th>Criação</th><th /></tr></thead><tbody>
        {result?.items.map((transfer) => <tr key={transfer.id}><td><strong>{transfer.number}</strong></td><td>{transfer.originStoreCode}<small className="table-subtitle">{transfer.originStoreName}</small></td><td>{transfer.destinationStoreCode}<small className="table-subtitle">{transfer.destinationStoreName}</small></td><td><span className={`status-pill transfer-${transfer.status.toLowerCase()}`}>{transferStatusLabels[transfer.status]}</span></td><td>{transfer.itemCount}</td><td>{quantity.format(transfer.totalQuantity)}</td><td>{dateTime.format(new Date(transfer.createdAtUtc))}</td><td className="table-actions"><NavLink className="link-button" to={`/inventory/transfers/${transfer.id}`}>Detalhes</NavLink></td></tr>)}
        {result?.items.length === 0 && <tr><td colSpan={8}>Nenhuma transferência encontrada.</td></tr>}
      </tbody></table></div>
      {result && <Pagination page={result.page} totalPages={result.totalPages} totalCount={result.totalCount} onPageChange={(page) => setQuery((current) => ({ ...current, page }))} />}
    </section>
  )
}
