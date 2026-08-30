import { useEffect, useState } from 'react'
import { NavLink, useLocation, useParams } from 'react-router-dom'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { getSale } from './salesService'
import { paymentMethodLabels, type SaleDetails } from './salesTypes'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function SaleDetailsPage() {
  const { saleId } = useParams()
  const location = useLocation()
  const id = Number(saleId)
  const [sale, setSale] = useState<SaleDetails | null>(null)
  const [error, setError] = useState<string | null>(null)
  const message = (location.state as { message?: string } | null)?.message

  useEffect(() => {
    const controller = new AbortController()
    if (!Number.isInteger(id) || id <= 0) {
      // oxlint-disable-next-line react/set-state-in-effect
      setError('Identificador de venda inválido.')
      return () => controller.abort()
    }
    void getSale(id, controller.signal).then(setSale).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [id])

  if (!sale && !error) return <section className="panel">Carregando venda…</section>
  if (!sale) return <section className="panel"><div className="form-error">{error}</div></section>

  return (
    <section className="sale-details">
      {message && <div className="form-success page-message">{message}</div>}
      <article className="panel">
        <div className="section-heading"><div><span className="eyebrow">Venda finalizada e imutável</span><h2>{sale.number}</h2></div><span className="status-pill purchase-finalized">Finalizada</span></div>
        <dl className="purchase-summary-grid">
          <div><dt>Loja</dt><dd>{sale.storeCode} · {sale.storeName}</dd></div>
          <div><dt>Caixa</dt><dd>{sale.cashRegisterCode} · sessão {sale.cashSessionId}</dd></div>
          <div><dt>Cliente</dt><dd>{sale.customerName ?? 'Consumidor não identificado'}</dd></div>
          <div><dt>Operador</dt><dd>{sale.createdByUserName}</dd></div>
          <div><dt>Finalização</dt><dd>{dateTime.format(new Date(sale.finalizedAtUtc))}</dd></div>
          <div><dt>Total</dt><dd>{money.format(sale.totalAmount)}</dd></div>
        </dl>
      </article>
      <article className="panel">
        <span className="eyebrow">Preços históricos da operação</span><h2>Itens</h2>
        <div className="table-scroll"><table><thead><tr><th>Produto</th><th>Quantidade</th><th>Preço unitário</th><th>Subtotal</th></tr></thead><tbody>{sale.items.map((item) => <tr key={item.id}><td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td><td>{quantity.format(item.quantity)}</td><td>{money.format(item.unitPrice)}</td><td>{money.format(item.lineSubtotal)}</td></tr>)}</tbody></table></div>
      </article>
      <div className="sale-settlement-grid">
        <article className="panel"><span className="eyebrow">Conciliação</span><h2>Pagamentos</h2><ul className="settlement-list">{sale.payments.map((payment) => <li key={payment.id}><span>{paymentMethodLabels[payment.method]}{payment.reference && <small>{payment.reference}</small>}</span><strong>{money.format(payment.amount)}</strong></li>)}</ul></article>
        <article className="panel"><span className="eyebrow">Cálculo financeiro</span><h2>Resumo</h2><dl className="sale-total-summary"><div><dt>Subtotal</dt><dd>{money.format(sale.subtotal)}</dd></div><div><dt>Desconto</dt><dd>{money.format(sale.discountAmount)}</dd></div><div><dt>Total</dt><dd>{money.format(sale.totalAmount)}</dd></div></dl></article>
      </div>
      <div className="details-actions"><NavLink className="button button-secondary" to="/sales">Voltar ao histórico</NavLink><NavLink className="button button-primary" to="/sales/new">Nova venda</NavLink></div>
    </section>
  )
}
