import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { listStockPositions, updateMinimumStock } from './inventoryService'
import type { PagedPositions, PositionQuery, StockPosition } from './inventoryTypes'

type MinimumForm = { minimumQuantity: number }

const initialQuery: PositionQuery = {
  page: 1,
  pageSize: 20,
  sortBy: 'product',
  sortDirection: 'asc',
}

const quantity = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 3,
})

export function StockPositionsPage() {
  const { user, hasPermission } = useAuth()
  const navigate = useNavigate()
  const canManage = hasPermission(permissions.inventoryManage)
  const [result, setResult] = useState<PagedPositions | null>(null)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [storeFilter, setStoreFilter] = useState('all')
  const [minimumFilter, setMinimumFilter] = useState('all')
  const [editing, setEditing] = useState<StockPosition | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<MinimumForm>()

  const load = useCallback(async (activeQuery: PositionQuery, signal?: AbortSignal) => {
    setResult(await listStockPositions(activeQuery, signal))
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
      belowMinimum: minimumFilter === 'all' ? undefined : minimumFilter === 'below',
    }))
  }

  function editMinimum(position: StockPosition) {
    setEditing(position)
    reset({ minimumQuantity: position.minimumQuantity })
    setError(null)
    setMessage(null)
  }

  return (
    <section className="management-layout management-layout-inventory">
      <article className="panel table-panel">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Estoque por produto + loja</span>
            <h2>Posição atual</h2>
          </div>
          <span className="count-badge">{result?.totalCount ?? 0} posições</span>
        </div>

        <div className="filter-grid filter-grid-inventory">
          <label>
            Busca
            <input
              value={search}
              placeholder="Código, barras ou produto"
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
            Nível
            <select value={minimumFilter} onChange={(event) => setMinimumFilter(event.target.value)}>
              <option value="all">Todos</option>
              <option value="below">Baixo estoque</option>
              <option value="normal">Acima do mínimo</option>
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
              <option value="product:asc">Produto A–Z</option>
              <option value="store:asc">Loja A–Z</option>
              <option value="quantity:asc">Menor quantidade</option>
              <option value="quantity:desc">Maior quantidade</option>
              <option value="minimum:desc">Maior estoque mínimo</option>
            </select>
          </label>
          <button className="button button-secondary filter-action" type="button" onClick={applyFilters}>
            Aplicar
          </button>
        </div>

        {error && <div className="form-error page-message">{error}</div>}
        {message && <div className="form-success page-message">{message}</div>}
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Produto</th>
                <th>Loja</th>
                <th>Disponível</th>
                <th>Mínimo</th>
                <th>Nível</th>
                {canManage && <th />}
              </tr>
            </thead>
            <tbody>
              {result?.items.map((position) => (
                <tr key={`${position.storeId}:${position.productId}`}>
                  <td>
                    <strong>{position.productName}</strong>
                    <small className="table-subtitle">
                      {position.productCode}{position.productBarcode ? ` · ${position.productBarcode}` : ''}
                    </small>
                  </td>
                  <td>{position.storeCode}<small className="table-subtitle">{position.storeName}</small></td>
                  <td className="quantity-cell">{quantity.format(position.quantityOnHand)}</td>
                  <td>{quantity.format(position.minimumQuantity)}</td>
                  <td>
                    <span className={position.isBelowMinimum ? 'status-pill stock-low' : 'status-pill active'}>
                      {position.isBelowMinimum ? 'Baixo' : 'Normal'}
                    </span>
                  </td>
                  {canManage && (
                    <td className="table-actions inventory-actions">
                      <button className="link-button" type="button" onClick={() => editMinimum(position)}>
                        Mínimo
                      </button>
                      <button
                        className="link-button"
                        type="button"
                        onClick={() => navigate(`/inventory/adjustments?storeId=${position.storeId}&productId=${position.productId}`)}
                      >
                        Ajustar
                      </button>
                    </td>
                  )}
                </tr>
              ))}
              {result?.items.length === 0 && (
                <tr><td colSpan={canManage ? 6 : 5}>Nenhuma posição encontrada para os filtros.</td></tr>
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
      </article>

      {canManage && editing && (
        <aside className="panel form-panel">
          <span className="eyebrow">Proteção por rowversion</span>
          <h2>Estoque mínimo</h2>
          <p>{editing.productName} · {editing.storeName}</p>
          <form
            className="form-stack"
            onSubmit={handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              try {
                await updateMinimumStock(
                  editing.storeId,
                  editing.productId,
                  values.minimumQuantity,
                  editing.rowVersion,
                )
                setMessage('Estoque mínimo atualizado.')
                setEditing(null)
                await load(query)
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              }
            })}
          >
            <label>
              Quantidade mínima
              <input
                type="number"
                min="0"
                max="999999999999.999"
                step="0.001"
                {...register('minimumQuantity', {
                  valueAsNumber: true,
                  min: { value: 0, message: 'O mínimo não pode ser negativo.' },
                  required: 'Informe a quantidade mínima.',
                })}
              />
              {errors.minimumQuantity && <small className="field-error">{errors.minimumQuantity.message}</small>}
            </label>
            <div className="button-row">
              <button className="button button-primary" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Salvando…' : 'Salvar mínimo'}
              </button>
              <button className="button button-secondary" type="button" onClick={() => setEditing(null)}>
                Cancelar
              </button>
            </div>
          </form>
        </aside>
      )}
    </section>
  )
}
