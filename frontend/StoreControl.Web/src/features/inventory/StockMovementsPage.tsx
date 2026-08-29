import { useCallback, useEffect, useState } from 'react'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { listStockMovements } from './inventoryService'
import {
  movementLabels,
  type MovementQuery,
  type PagedMovements,
  type StockMovementType,
} from './inventoryTypes'

const initialQuery: MovementQuery = {
  page: 1,
  pageSize: 20,
  sortBy: 'occurredAt',
  sortDirection: 'desc',
}

const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function StockMovementsPage() {
  const { user } = useAuth()
  const [result, setResult] = useState<PagedMovements | null>(null)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [storeFilter, setStoreFilter] = useState('all')
  const [typeFilter, setTypeFilter] = useState('all')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (activeQuery: MovementQuery, signal?: AbortSignal) => {
    setResult(await listStockMovements(activeQuery, signal))
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
    setError(null)
    setQuery((current) => ({
      ...current,
      page: 1,
      search: search.trim() || undefined,
      storeId: storeFilter === 'all' ? undefined : Number(storeFilter),
      type: typeFilter === 'all' ? undefined : typeFilter as StockMovementType,
      fromUtc: fromDate ? startOfLocalDayAsUtc(fromDate) : undefined,
      toUtc: toDate ? endOfLocalDayAsUtc(toDate) : undefined,
    }))
  }

  return (
    <section className="panel table-panel inventory-wide">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Ledger imutável</span>
          <h2>Movimentações de estoque</h2>
        </div>
        <span className="count-badge">{result?.totalCount ?? 0} movimentos</span>
      </div>

      <div className="filter-grid movement-filters">
        <label>
          Busca
          <input
            value={search}
            placeholder="Produto, loja, motivo ou referência"
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
          Tipo
          <select value={typeFilter} onChange={(event) => setTypeFilter(event.target.value)}>
            <option value="all">Todos</option>
            {Object.entries(movementLabels).map(([value, label]) => (
              <option value={value} key={value}>{label}</option>
            ))}
          </select>
        </label>
        <label>
          De
          <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        </label>
        <label>
          Até
          <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        </label>
        <button className="button button-secondary filter-action" type="button" onClick={applyFilters}>
          Aplicar
        </button>
      </div>

      {error && <div className="form-error page-message">{error}</div>}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Data</th>
              <th>Produto / loja</th>
              <th>Tipo</th>
              <th>Alteração</th>
              <th>Saldo</th>
              <th>Rastreabilidade</th>
            </tr>
          </thead>
          <tbody>
            {result?.items.map((movement) => (
              <tr key={movement.id}>
                <td>{dateTime.format(new Date(movement.occurredAtUtc))}<small className="table-subtitle">#{movement.id}</small></td>
                <td>
                  <strong>{movement.productName}</strong>
                  <small className="table-subtitle">{movement.productCode} · {movement.storeCode}</small>
                </td>
                <td>{movementLabels[movement.type] ?? movement.type}</td>
                <td className={movement.quantityChange > 0 ? 'quantity-positive' : 'quantity-negative'}>
                  {movement.quantityChange > 0 ? '+' : ''}{quantity.format(movement.quantityChange)}
                </td>
                <td>
                  {quantity.format(movement.quantityBefore)} → {quantity.format(movement.quantityAfter)}
                </td>
                <td>
                  {movement.reason}
                  <small className="table-subtitle">Por {movement.createdByUserName}</small>
                  <small className="table-subtitle trace-value" title={movement.correlationId}>
                    Correlação {movement.correlationId}
                  </small>
                  {movement.referenceType && (
                    <small className="table-subtitle">
                      {movement.referenceType}{movement.referenceId ? ` · ${movement.referenceId}` : ''}
                    </small>
                  )}
                </td>
              </tr>
            ))}
            {result?.items.length === 0 && (
              <tr><td colSpan={6}>Nenhuma movimentação encontrada para os filtros.</td></tr>
            )}
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

function startOfLocalDayAsUtc(date: string) {
  return new Date(`${date}T00:00:00`).toISOString()
}

function endOfLocalDayAsUtc(date: string) {
  return new Date(`${date}T23:59:59.999`).toISOString()
}
