import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { NavLink, useLocation, useParams } from 'react-router-dom'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { getCurrentCashSession, listCashRegisters } from '../cash/cashService'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { cancelSale, getSale } from './salesService'
import { paymentMethodLabels, type SaleDetails } from './salesTypes'

type CancellationForm = { cashSessionId: number; reason: string }
type OpenSessionOption = { id: number; label: string }

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function SaleDetailsPage() {
  const { saleId } = useParams()
  const location = useLocation()
  const { hasPermission } = useAuth()
  const id = Number(saleId)
  const [sale, setSale] = useState<SaleDetails | null>(null)
  const [openSessions, setOpenSessions] = useState<OpenSessionOption[]>([])
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [cancellationOperationId, setCancellationOperationId] = useState<string | null>(null)
  const message = (location.state as { message?: string } | null)?.message
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<CancellationForm>({ defaultValues: { cashSessionId: 0, reason: '' } })

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

  useEffect(() => {
    if (!sale || sale.status !== 'Finalized' || !hasPermission(permissions.salesManage)) return
    const controller = new AbortController()
    void listCashRegisters(sale.storeId, controller.signal)
      .then(async (registers) => {
        const sessions = await Promise.all(registers.map(async (cashRegister) => ({
          cashRegister,
          session: await getCurrentCashSession(cashRegister.id, controller.signal),
        })))
        const options = sessions
          .filter((item) => item.session !== undefined)
          .map((item) => ({
            id: item.session!.id,
            label: `${item.cashRegister.code} · sessão ${item.session!.id} · ${money.format(item.session!.expectedCashAmount)}`,
          }))
        setOpenSessions(options)
        if (options.length === 1) setValue('cashSessionId', options[0].id)
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [hasPermission, sale, setValue])

  if (!sale && !error) return <section className="panel">Carregando venda…</section>
  if (!sale) return <section className="panel"><div className="form-error">{error}</div></section>

  const isCancelled = sale.status === 'Cancelled'

  return (
    <section className="sale-details">
      {(message || success) && <div className="form-success page-message">{success ?? message}</div>}
      {error && <div className="form-error page-message">{error}</div>}
      <article className="panel">
        <div className="section-heading">
          <div><span className="eyebrow">Venda preservada com trilha de auditoria</span><h2>{sale.number}</h2></div>
          <span className={`status-pill purchase-${isCancelled ? 'cancelled' : 'finalized'}`}>
            {isCancelled ? 'Cancelada' : 'Finalizada'}
          </span>
        </div>
        <dl className="purchase-summary-grid">
          <div><dt>Loja</dt><dd>{sale.storeCode} · {sale.storeName}</dd></div>
          <div><dt>Caixa da venda</dt><dd>{sale.cashRegisterCode} · sessão {sale.cashSessionId}</dd></div>
          <div><dt>Cliente</dt><dd>{sale.customerName ?? 'Consumidor não identificado'}</dd></div>
          <div><dt>Operador</dt><dd>{sale.createdByUserName}</dd></div>
          <div><dt>Finalização</dt><dd>{dateTime.format(new Date(sale.finalizedAtUtc))}</dd></div>
          <div><dt>Total</dt><dd>{money.format(sale.totalAmount)}</dd></div>
          {isCancelled && <>
            <div><dt>Cancelamento</dt><dd>{dateTime.format(new Date(sale.cancelledAtUtc!))}</dd></div>
            <div><dt>Responsável</dt><dd>{sale.cancelledByUserName}</dd></div>
            <div><dt>Caixa do estorno</dt><dd>{sale.cancellationCashRegisterCode} · sessão {sale.cancellationCashSessionId}</dd></div>
            <div><dt>Motivo</dt><dd>{sale.cancellationReason}</dd></div>
          </>}
        </dl>
      </article>

      <article className="panel">
        <span className="eyebrow">Preços históricos da operação</span><h2>Itens</h2>
        <div className="table-scroll"><table><thead><tr><th>Produto</th><th>Quantidade</th><th>Preço unitário</th><th>Subtotal</th></tr></thead><tbody>{sale.items.map((item) => <tr key={item.id}><td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td><td>{quantity.format(item.quantity)}</td><td>{money.format(item.unitPrice)}</td><td>{money.format(item.lineSubtotal)}</td></tr>)}</tbody></table></div>
      </article>

      <div className="sale-settlement-grid">
        <article className="panel"><span className="eyebrow">Conciliação</span><h2>Pagamentos originais</h2><ul className="settlement-list">{sale.payments.map((payment) => <li key={payment.id}><span>{paymentMethodLabels[payment.method]}{payment.reference && <small>{payment.reference}</small>}</span><strong>{money.format(payment.amount)}</strong></li>)}</ul></article>
        <article className="panel"><span className="eyebrow">Cálculo financeiro</span><h2>Resumo</h2><dl className="sale-total-summary"><div><dt>Subtotal</dt><dd>{money.format(sale.subtotal)}</dd></div><div><dt>Desconto</dt><dd>{money.format(sale.discountAmount)}</dd></div><div><dt>Total</dt><dd>{money.format(sale.totalAmount)}</dd></div></dl></article>
      </div>

      <article className="panel purchase-timeline-panel">
        <span className="eyebrow">Auditoria</span><h2>Histórico de status</h2>
        <ol className="purchase-timeline">{sale.statusHistory.map((history) => <li key={history.id}><strong>{history.status === 'Cancelled' ? 'Cancelada' : 'Finalizada'}</strong><span>{history.description}</span><small>{history.changedByUserName} · {dateTime.format(new Date(history.changedAtUtc))}</small></li>)}</ol>
      </article>

      {!isCancelled && hasPermission(permissions.salesManage) && (
        <article className="panel sale-cancellation-panel">
          <span className="eyebrow">Operação transacional e irreversível</span><h2>Cancelar venda</h2>
          <p className="muted-text">O cancelamento devolve os itens ao estoque e registra estornos dos pagamentos no caixa aberto selecionado.</p>
          <form className="form-columns" onSubmit={handleSubmit(async (values) => {
            setError(null)
            setSuccess(null)
            const operationId = cancellationOperationId ?? crypto.randomUUID()
            setCancellationOperationId(operationId)
            try {
              const result = await cancelSale(sale.id, operationId, {
                cashSessionId: values.cashSessionId,
                rowVersion: sale.rowVersion,
                reason: values.reason.trim(),
              })
              setSale(result.sale)
              setCancellationOperationId(null)
              setSuccess(result.wasAlreadyProcessed
                ? 'Cancelamento já confirmado; os dados foram recarregados.'
                : 'Venda cancelada. Estoque e caixa foram compensados na mesma transação.')
            } catch (requestError) {
              setError(getProblemMessage(requestError))
            }
          })}>
            <label>Sessão aberta para o estorno<select {...register('cashSessionId', { valueAsNumber: true, min: { value: 1, message: 'Selecione uma sessão de caixa aberta.' } })}><option value={0}>Selecione</option>{openSessions.map((session) => <option value={session.id} key={session.id}>{session.label}</option>)}</select>{errors.cashSessionId && <small className="field-error">{errors.cashSessionId.message}</small>}{openSessions.length === 0 && <small>Abra um caixa nesta loja antes de cancelar.</small>}</label>
            <label>Motivo<textarea rows={3} maxLength={500} {...register('reason', { required: 'Informe o motivo do cancelamento.', validate: (value) => value.trim().length > 0 || 'Informe o motivo do cancelamento.' })} />{errors.reason && <small className="field-error">{errors.reason.message}</small>}</label>
            <button className="button button-danger" type="submit" disabled={isSubmitting || openSessions.length === 0}>{isSubmitting ? 'Cancelando…' : 'Confirmar cancelamento'}</button>
          </form>
        </article>
      )}

      <div className="details-actions"><NavLink className="button button-secondary" to="/sales">Voltar ao histórico</NavLink><NavLink className="button button-primary" to="/sales/new">Nova venda</NavLink></div>
    </section>
  )
}
