import { useEffect, useState } from 'react'
import { NavLink, useParams } from 'react-router-dom'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { paymentMethodLabels } from '../sales/salesTypes'
import { getCashSession } from './cashService'
import type { CashSessionDetails } from './cashTypes'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function CashSessionDetailsPage() {
  const { cashSessionId } = useParams()
  const id = Number(cashSessionId)
  const invalidId = !Number.isInteger(id) || id <= 0
  const [session, setSession] = useState<CashSessionDetails | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    if (invalidId) return () => controller.abort()
    void getCashSession(id, controller.signal).then(setSession).catch((requestError) => { if (!controller.signal.aborted) setError(getProblemMessage(requestError)) })
    return () => controller.abort()
  }, [id, invalidId])
  if (invalidId) return <section className="panel"><div className="form-error">Identificador de sessão inválido.</div></section>
  if (!session && !error) return <section className="panel">Carregando sessão…</section>
  if (!session) return <section className="panel"><div className="form-error">{error}</div></section>

  return <section className="sale-details">
    <article className="panel"><div className="section-heading"><div><span className="eyebrow">Sessão de caixa {session.id}</span><h2>{session.storeCode} · {session.cashRegisterCode}</h2></div><span className={`status-pill ${session.status === 'Open' ? 'purchase-finalized' : ''}`}>{session.status === 'Open' ? 'Aberto' : 'Fechado'}</span></div><dl className="purchase-summary-grid"><div><dt>Abertura</dt><dd>{money.format(session.openingAmount)}</dd></div><div><dt>Dinheiro esperado</dt><dd>{money.format(session.expectedCashAmount)}</dd></div><div><dt>Dinheiro declarado</dt><dd>{session.declaredClosingAmount === null ? '—' : money.format(session.declaredClosingAmount)}</dd></div><div><dt>Diferença</dt><dd>{session.differenceAmount === null ? '—' : money.format(session.differenceAmount)}</dd></div><div><dt>Aberto por</dt><dd>{session.openedByUserName} · {dateTime.format(new Date(session.openedAtUtc))}</dd></div><div><dt>Fechado por</dt><dd>{session.closedAtUtc ? `${session.closedByUserName} · ${dateTime.format(new Date(session.closedAtUtc))}` : '—'}</dd></div></dl></article>
    <article className="panel"><span className="eyebrow">Histórico completo</span><h2>Movimentações</h2><div className="table-scroll"><table><thead><tr><th>Data</th><th>Tipo</th><th>Pagamento</th><th>Motivo / referência</th><th>Operador</th><th>Valor</th></tr></thead><tbody>{session.movements.map((movement) => <tr key={movement.id}><td>{dateTime.format(new Date(movement.occurredAtUtc))}</td><td>{movement.type}</td><td>{movement.paymentMethod ? paymentMethodLabels[movement.paymentMethod] : '—'}</td><td>{movement.reason}{movement.referenceId && <small className="table-subtitle">{movement.referenceType} · {movement.referenceId}</small>}</td><td>{movement.createdByUserName}</td><td className={movement.amount < 0 ? 'quantity-negative' : 'quantity-positive'}>{money.format(movement.amount)}</td></tr>)}</tbody></table></div></article>
    <div className="details-actions"><NavLink className="button button-secondary" to="/cash/history">Voltar ao histórico</NavLink>{session.status === 'Open' && <NavLink className="button button-primary" to="/cash/current">Operar caixa</NavLink>}</div>
  </section>
}
