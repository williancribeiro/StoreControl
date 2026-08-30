import { useCallback, useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { Pagination } from '../registrations/Pagination'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { listCashRegisters, listCashSessions } from './cashService'
import type { CashRegister, CashSessionQuery, CashSessionStatus, PagedCashSessions } from './cashTypes'

const initialQuery: CashSessionQuery = { page: 1, pageSize: 20, sortBy: 'openedAt', sortDirection: 'desc' }
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function CashHistoryPage() {
  const [registers, setRegisters] = useState<CashRegister[]>([])
  const [result, setResult] = useState<PagedCashSessions | null>(null)
  const [query, setQuery] = useState(initialQuery)
  const [registerFilter, setRegisterFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(async (active: CashSessionQuery, signal?: AbortSignal) => setResult(await listCashSessions(active, signal)), [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void Promise.all([listCashRegisters(undefined, controller.signal), load(query, controller.signal)])
      .then(([available]) => setRegisters(available))
      .catch((requestError) => { if (!controller.signal.aborted) setError(getProblemMessage(requestError)) })
    return () => controller.abort()
  }, [load, query])

  return <section className="panel table-panel purchases-wide">
    <div className="section-heading"><div><span className="eyebrow">Aberturas, fechamentos e diferenças</span><h2>Histórico de caixas</h2></div><NavLink className="button button-primary" to="/cash/current">Operação atual</NavLink></div>
    <div className="filter-grid cash-history-filters">
      <label>Caixa<select value={registerFilter} onChange={(event) => setRegisterFilter(event.target.value)}><option value="all">Todos acessíveis</option>{registers.map((item) => <option key={item.id} value={item.id}>{item.storeCode} · {item.code}</option>)}</select></label>
      <label>Status<select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}><option value="all">Todos</option><option value="Open">Aberto</option><option value="Closed">Fechado</option></select></label>
      <label>Ordenar<select value={`${query.sortBy}:${query.sortDirection}`} onChange={(event) => { const [sortBy, sortDirection] = event.target.value.split(':'); setQuery((current) => ({ ...current, page: 1, sortBy, sortDirection: sortDirection as 'asc' | 'desc' })) }}><option value="openedAt:desc">Abertura mais recente</option><option value="openedAt:asc">Abertura mais antiga</option><option value="difference:desc">Maior diferença</option></select></label>
      <button className="button button-secondary filter-action" type="button" onClick={() => setQuery((current) => ({ ...current, page: 1, cashRegisterId: registerFilter === 'all' ? undefined : Number(registerFilter), status: statusFilter === 'all' ? undefined : statusFilter as CashSessionStatus }))}>Aplicar</button>
    </div>
    {error && <div className="form-error page-message">{error}</div>}
    <div className="table-scroll"><table><thead><tr><th>Loja / caixa</th><th>Status</th><th>Abertura</th><th>Fechamento</th><th>Operador</th><th>Saldo esperado</th><th>Diferença</th><th /></tr></thead><tbody>{result?.items.map((session) => <tr key={session.id}><td><strong>{session.storeCode} · {session.cashRegisterCode}</strong><small className="table-subtitle">Sessão {session.id}</small></td><td><span className={`status-pill ${session.status === 'Open' ? 'purchase-finalized' : ''}`}>{session.status === 'Open' ? 'Aberto' : 'Fechado'}</span></td><td>{dateTime.format(new Date(session.openedAtUtc))}</td><td>{session.closedAtUtc ? dateTime.format(new Date(session.closedAtUtc)) : '—'}</td><td>{session.openedByUserName}</td><td>{money.format(session.expectedCashAmount)}</td><td>{session.differenceAmount === null ? '—' : money.format(session.differenceAmount)}</td><td><NavLink className="link-button" to={`/cash/sessions/${session.id}`}>Detalhes</NavLink></td></tr>)}{result?.items.length === 0 && <tr><td colSpan={8}>Nenhuma sessão encontrada.</td></tr>}</tbody></table></div>
    {result && <Pagination page={result.page} totalPages={result.totalPages} totalCount={result.totalCount} onPageChange={(page) => setQuery((current) => ({ ...current, page }))} />}
  </section>
}
