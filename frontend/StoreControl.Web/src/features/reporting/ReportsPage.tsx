import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { movementLabels, type StockMovementType } from '../inventory/inventoryTypes'
import { purchaseStatusLabels, type PurchaseStatus } from '../purchasing/purchaseTypes'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { useAuth } from '../auth/useAuth'
import {
  getInventoryReport,
  getMovementReport,
  getPurchaseReport,
  getSalesReport,
} from './reportingService'
import type {
  InventoryReportQuery,
  MovementReport,
  PagedInventoryReport,
  PurchaseReport,
  ReportPeriodQuery,
  SalesReport,
} from './reportingTypes'

type ReportFilters = {
  fromDate: string
  toDate: string
  storeId: string
  search: string
  stockLevel: 'all' | 'low' | 'normal'
}

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const today = new Date().toISOString().slice(0, 10)
const thirtyDaysAgo = new Date(Date.now() - 29 * 86_400_000).toISOString().slice(0, 10)
const initialPeriod: ReportPeriodQuery = { fromDate: thirtyDaysAgo, toDate: today }
const initialInventory: InventoryReportQuery = {
  page: 1,
  pageSize: 20,
  sortBy: 'product',
  sortDirection: 'asc',
}

export function ReportsPage() {
  const { user } = useAuth()
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [periodQuery, setPeriodQuery] = useState(initialPeriod)
  const [inventoryQuery, setInventoryQuery] = useState(initialInventory)
  const [sales, setSales] = useState<SalesReport | null>(null)
  const [movements, setMovements] = useState<MovementReport | null>(null)
  const [purchases, setPurchases] = useState<PurchaseReport | null>(null)
  const [inventory, setInventory] = useState<PagedInventoryReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit } = useForm<ReportFilters>({
    defaultValues: {
      fromDate: initialPeriod.fromDate,
      toDate: initialPeriod.toDate,
      storeId: 'all',
      search: '',
      stockLevel: 'all',
    },
  })

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

  useEffect(() => {
    const controller = new AbortController()
    void Promise.all([
      getSalesReport(periodQuery, controller.signal),
      getMovementReport(periodQuery, controller.signal),
      getPurchaseReport(periodQuery, controller.signal),
    ])
      .then(([salesResult, movementResult, purchaseResult]) => {
        setSales(salesResult)
        setMovements(movementResult)
        setPurchases(purchaseResult)
        setError(null)
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [periodQuery])

  useEffect(() => {
    const controller = new AbortController()
    void getInventoryReport(inventoryQuery, controller.signal)
      .then((result) => {
        setInventory(result)
        setError(null)
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [inventoryQuery])

  function applyFilters(values: ReportFilters) {
    const storeId = values.storeId === 'all' ? undefined : Number(values.storeId)
    setPeriodQuery({ fromDate: values.fromDate, toDate: values.toDate, storeId })
    setInventoryQuery((current) => ({
      ...current,
      page: 1,
      search: values.search.trim() || undefined,
      storeId,
      belowMinimum: values.stockLevel === 'all' ? undefined : values.stockLevel === 'low',
    }))
  }

  return (
    <section className="reports-layout">
      <form className="panel report-filters" onSubmit={handleSubmit(applyFilters)}>
        <div>
          <span className="eyebrow">Consultas somente leitura</span>
          <h2>Filtros dos relatórios</h2>
        </div>
        <label>De<input type="date" {...register('fromDate', { required: true })} /></label>
        <label>Até<input type="date" {...register('toDate', { required: true })} /></label>
        <label>
          Loja
          <select {...register('storeId')}>
            <option value="all">Todas acessíveis</option>
            {stores.map((store) => <option value={store.id} key={store.id}>{store.code} · {store.name}</option>)}
          </select>
        </label>
        <label>Busca no estoque<input placeholder="Produto ou categoria" {...register('search')} /></label>
        <label>
          Nível do estoque
          <select {...register('stockLevel')}>
            <option value="all">Todos</option>
            <option value="low">Baixo estoque</option>
            <option value="normal">Acima do mínimo</option>
          </select>
        </label>
        <button className="button button-primary" type="submit">Aplicar filtros</button>
      </form>

      {error && <div className="form-error page-message">{error}</div>}
      {(!sales || !movements || !purchases || !inventory) && !error && (
        <article className="panel status-message">Carregando relatórios…</article>
      )}

      {sales && movements && purchases && inventory && (
        <>
          <div className="report-summary-grid">
            <SummaryCard label="Vendas" value={quantity.format(sales.saleCount)} detail={money.format(sales.revenue)} />
            <SummaryCard label="Ticket médio" value={money.format(sales.averageTicket)} detail={`${money.format(sales.discountAmount)} em descontos`} />
            <SummaryCard label="Movimentações" value={quantity.format(movements.movementCount)} detail={`${quantity.format(movements.entryQuantity)} entradas`} />
            <SummaryCard label="Compras" value={quantity.format(purchases.orderCount)} detail={money.format(purchases.orderedAmount)} />
          </div>

          <div className="report-sections">
            <ReportPanel eyebrow="Período e loja" title="Vendas por loja">
              <table>
                <thead><tr><th>Loja</th><th>Vendas</th><th>Faturamento</th><th>Ticket médio</th></tr></thead>
                <tbody>
                  {sales.byStore.map((row) => <tr key={row.storeId}><td><strong>{row.storeCode}</strong><small className="table-subtitle">{row.storeName}</small></td><td>{row.saleCount}</td><td>{money.format(row.revenue)}</td><td>{money.format(row.averageTicket)}</td></tr>)}
                  {sales.byStore.length === 0 && <tr><td colSpan={4}>Nenhuma venda válida no período.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>

            <ReportPanel eyebrow="Ranking por quantidade" title="Produtos mais vendidos">
              <table>
                <thead><tr><th>Produto</th><th>Quantidade</th><th>Vendas</th><th>Valor bruto</th></tr></thead>
                <tbody>
                  {sales.topSellingProducts.map((row) => <tr key={row.productId}><td><strong>{row.productName}</strong><small className="table-subtitle">{row.productCode}</small></td><td>{quantity.format(row.quantitySold)}</td><td>{row.saleCount}</td><td>{money.format(row.grossRevenue)}</td></tr>)}
                  {sales.topSellingProducts.length === 0 && <tr><td colSpan={4}>Sem produtos vendidos no período.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>

            <ReportPanel eyebrow="Série diária" title="Evolução de vendas" wide>
              <table>
                <thead><tr><th>Data</th><th>Vendas</th><th>Faturamento</th></tr></thead>
                <tbody>
                  {sales.dailyEvolution.map((row) => <tr key={row.date}><td>{formatDate(row.date)}</td><td>{row.saleCount}</td><td>{money.format(row.revenue)}</td></tr>)}
                  {sales.dailyEvolution.length === 0 && <tr><td colSpan={3}>Sem evolução para exibir.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>

            <ReportPanel eyebrow="View reporting.vw_StockPosition" title="Posição de estoque" wide>
              <table>
                <thead><tr><th>Produto</th><th>Categoria</th><th>Loja</th><th>Disponível</th><th>Mínimo</th><th>Nível</th></tr></thead>
                <tbody>
                  {inventory.items.map((row) => <tr key={`${row.storeId}:${row.productId}`}><td><strong>{row.productName}</strong><small className="table-subtitle">{row.productCode}</small></td><td>{row.categoryName}</td><td>{row.storeCode}</td><td>{quantity.format(row.quantityOnHand)}</td><td>{quantity.format(row.minimumQuantity)}</td><td><span className={`status-pill ${row.isBelowMinimum ? 'stock-low' : 'active'}`}>{row.isBelowMinimum ? 'Baixo' : 'Normal'}</span></td></tr>)}
                  {inventory.items.length === 0 && <tr><td colSpan={6}>Nenhuma posição encontrada.</td></tr>}
                </tbody>
              </table>
              <Pagination page={inventory.page} totalPages={inventory.totalPages} totalCount={inventory.totalCount} onPageChange={(page) => setInventoryQuery((current) => ({ ...current, page }))} />
            </ReportPanel>

            <ReportPanel eyebrow="Ledger de estoque" title="Movimentações por tipo">
              <table>
                <thead><tr><th>Tipo</th><th>Eventos</th><th>Entradas</th><th>Saídas</th></tr></thead>
                <tbody>
                  {movements.byType.map((row) => <tr key={row.type}><td>{movementLabels[row.type as StockMovementType] ?? row.type}</td><td>{row.movementCount}</td><td className="quantity-positive">{quantity.format(row.entryQuantity)}</td><td className="quantity-negative">{quantity.format(row.exitQuantity)}</td></tr>)}
                  {movements.byType.length === 0 && <tr><td colSpan={4}>Sem movimentações no período.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>

            <ReportPanel eyebrow="Pedidos criados" title="Compras por status">
              <table>
                <thead><tr><th>Status</th><th>Pedidos</th><th>Valor documentado</th></tr></thead>
                <tbody>
                  {purchases.byStatus.map((row) => <tr key={row.status}><td>{purchaseStatusLabels[row.status as PurchaseStatus] ?? row.status}</td><td>{row.orderCount}</td><td>{money.format(row.orderedAmount)}</td></tr>)}
                  {purchases.byStatus.length === 0 && <tr><td colSpan={3}>Sem compras no período.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>

            <ReportPanel eyebrow="Valores pedidos e recebidos" title="Compras por loja" wide>
              <table>
                <thead><tr><th>Loja</th><th>Pedidos</th><th>Valor não cancelado</th><th>Valor recebido</th></tr></thead>
                <tbody>
                  {purchases.byStore.map((row) => <tr key={row.storeId}><td><strong>{row.storeCode}</strong><small className="table-subtitle">{row.storeName}</small></td><td>{row.orderCount}</td><td>{money.format(row.orderedAmount)}</td><td>{money.format(row.receivedAmount)}</td></tr>)}
                  {purchases.byStore.length === 0 && <tr><td colSpan={4}>Sem compras para agrupar.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>

            <ReportPanel eyebrow="Fornecedores no período" title="Compras por fornecedor" wide>
              <table>
                <thead><tr><th>Fornecedor</th><th>Pedidos</th><th>Valor não cancelado</th></tr></thead>
                <tbody>
                  {purchases.bySupplier.map((row) => <tr key={row.supplierId}><td><strong>{row.supplierName}</strong><small className="table-subtitle">{row.supplierCode}</small></td><td>{row.orderCount}</td><td>{money.format(row.orderedAmount)}</td></tr>)}
                  {purchases.bySupplier.length === 0 && <tr><td colSpan={3}>Sem fornecedores para agrupar.</td></tr>}
                </tbody>
              </table>
            </ReportPanel>
          </div>
        </>
      )}
    </section>
  )
}

function SummaryCard({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <article className="panel report-summary-card"><span>{label}</span><strong>{value}</strong><small>{detail}</small></article>
}

function ReportPanel({ eyebrow, title, wide = false, children }: { eyebrow: string; title: string; wide?: boolean; children: React.ReactNode }) {
  return <article className={`panel table-panel report-panel${wide ? ' report-panel-wide' : ''}`}><span className="eyebrow">{eyebrow}</span><h2>{title}</h2><div className="table-scroll">{children}</div></article>
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString('pt-BR')
}
