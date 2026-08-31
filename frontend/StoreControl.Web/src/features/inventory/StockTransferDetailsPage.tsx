import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { NavLink, useLocation, useParams } from 'react-router-dom'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { getProblemMessage } from '../../shared/api/problemDetails'
import {
  cancelStockTransfer,
  dispatchStockTransfer,
  getStockTransfer,
  receiveStockTransfer,
} from './transferService'
import { transferStatusLabels, type StockTransferDetails } from './transferTypes'

type CancellationForm = { reason: string }
type ActionName = 'dispatch' | 'receive' | 'cancel'

const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function StockTransferDetailsPage() {
  const { stockTransferId } = useParams()
  const location = useLocation()
  const { hasPermission } = useAuth()
  const id = Number(stockTransferId)
  const [transfer, setTransfer] = useState<StockTransferDetails | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [activeAction, setActiveAction] = useState<ActionName | null>(null)
  const [operationIds, setOperationIds] = useState<Partial<Record<ActionName, string>>>({})
  const message = (location.state as { message?: string } | null)?.message
  const { register, handleSubmit, formState: { errors } } = useForm<CancellationForm>({
    defaultValues: { reason: '' },
  })

  useEffect(() => {
    const controller = new AbortController()
    if (!Number.isInteger(id) || id <= 0) {
      // oxlint-disable-next-line react/set-state-in-effect
      setError('Identificador de transferência inválido.')
      return () => controller.abort()
    }
    void getStockTransfer(id, controller.signal).then(setTransfer).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [id])

  async function runTransition(action: 'dispatch' | 'receive') {
    if (!transfer) return
    setError(null)
    setSuccess(null)
    setActiveAction(action)
    const operationId = operationIds[action] ?? crypto.randomUUID()
    setOperationIds((current) => ({ ...current, [action]: operationId }))
    try {
      const result = action === 'dispatch'
        ? await dispatchStockTransfer(transfer.id, operationId, transfer.rowVersion)
        : await receiveStockTransfer(transfer.id, operationId, transfer.rowVersion)
      setTransfer(result.transfer)
      setOperationIds((current) => ({ ...current, [action]: undefined }))
      setSuccess(result.wasAlreadyProcessed
        ? 'A operação já havia sido confirmada; o estado foi recarregado.'
        : action === 'dispatch'
          ? 'Expedição concluída. O estoque saiu da origem e está em trânsito.'
          : 'Recebimento concluído. O estoque entrou na loja de destino.')
    } catch (requestError) {
      setError(getProblemMessage(requestError))
    } finally {
      setActiveAction(null)
    }
  }

  if (!transfer && !error) return <section className="panel">Carregando transferência…</section>
  if (!transfer) return <section className="panel"><div className="form-error">{error}</div></section>

  const canManage = hasPermission(permissions.inventoryManage)

  return (
    <section className="purchase-details-layout">
      <div className="purchase-details-main">
        {(message || success) && <div className="form-success page-message">{success ?? message}</div>}
        {error && <div className="form-error page-message">{error}</div>}
        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Movimentação entre filiais</span><h2>{transfer.number}</h2></div><span className={`status-pill transfer-${transfer.status.toLowerCase()}`}>{transferStatusLabels[transfer.status]}</span></div>
          <dl className="purchase-summary-grid">
            <div><dt>Origem</dt><dd>{transfer.originStoreCode} · {transfer.originStoreName}</dd></div>
            <div><dt>Destino</dt><dd>{transfer.destinationStoreCode} · {transfer.destinationStoreName}</dd></div>
            <div><dt>Criada por</dt><dd>{transfer.createdByUserName}</dd></div>
            <div><dt>Criação</dt><dd>{dateTime.format(new Date(transfer.createdAtUtc))}</dd></div>
            {transfer.dispatchedAtUtc && <div><dt>Expedição</dt><dd>{transfer.dispatchedByUserName} · {dateTime.format(new Date(transfer.dispatchedAtUtc))}</dd></div>}
            {transfer.receivedAtUtc && <div><dt>Recebimento</dt><dd>{transfer.receivedByUserName} · {dateTime.format(new Date(transfer.receivedAtUtc))}</dd></div>}
            {transfer.cancelledAtUtc && <div><dt>Cancelamento</dt><dd>{transfer.cancelledByUserName} · {dateTime.format(new Date(transfer.cancelledAtUtc))}</dd></div>}
          </dl>
          {transfer.notes && <p className="purchase-notes"><strong>Observações:</strong> {transfer.notes}</p>}
          {transfer.cancellationReason && <p className="purchase-notes"><strong>Motivo do cancelamento:</strong> {transfer.cancellationReason}</p>}
        </article>

        <article className="panel">
          <span className="eyebrow">Snapshot da solicitação</span><h2>Itens</h2>
          <div className="table-scroll"><table><thead><tr><th>Produto</th><th>Quantidade</th></tr></thead><tbody>{transfer.items.map((item) => <tr key={item.id}><td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td><td>{quantity.format(item.quantity)}</td></tr>)}</tbody></table></div>
        </article>

        <article className="panel purchase-timeline-panel">
          <span className="eyebrow">Auditoria</span><h2>Histórico</h2>
          <ol className="purchase-timeline">{transfer.statusHistory.map((history) => <li key={history.id}><strong>{transferStatusLabels[history.status]}</strong><span>{history.description}</span><small>{history.changedByUserName} · {dateTime.format(new Date(history.changedAtUtc))}</small></li>)}</ol>
        </article>
      </div>

      <aside className="purchase-details-side">
        <article className="panel purchase-actions-panel">
          <span className="eyebrow">Próxima etapa física</span><h2>Ações</h2>
          {transfer.status === 'Created' && canManage && <>
            <p>Expedir baixa o estoque da origem. Confirme que a mercadoria está pronta para sair.</p>
            <button className="button button-primary" type="button" disabled={activeAction !== null} onClick={() => void runTransition('dispatch')}>{activeAction === 'dispatch' ? 'Expedindo…' : 'Expedir mercadoria'}</button>
            <form className="transfer-cancel-form" onSubmit={handleSubmit(async (values) => {
              setError(null)
              setSuccess(null)
              setActiveAction('cancel')
              const operationId = operationIds.cancel ?? crypto.randomUUID()
              setOperationIds((current) => ({ ...current, cancel: operationId }))
              try {
                const result = await cancelStockTransfer(transfer.id, operationId, transfer.rowVersion, values.reason.trim())
                setTransfer(result.transfer)
                setOperationIds((current) => ({ ...current, cancel: undefined }))
                setSuccess(result.wasAlreadyProcessed ? 'Cancelamento já confirmado.' : 'Transferência cancelada sem alterar o estoque.')
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              } finally {
                setActiveAction(null)
              }
            })}>
              <label>Motivo para cancelar<textarea rows={3} maxLength={500} {...register('reason', { required: 'Informe o motivo.', validate: (value) => value.trim().length > 0 || 'Informe o motivo.' })} />{errors.reason && <small className="field-error">{errors.reason.message}</small>}</label>
              <button className="button button-danger" type="submit" disabled={activeAction !== null}>{activeAction === 'cancel' ? 'Cancelando…' : 'Cancelar solicitação'}</button>
            </form>
          </>}
          {transfer.status === 'InTransit' && canManage && <><p>Receber registra a entrada integral dos itens no destino.</p><button className="button button-primary" type="button" disabled={activeAction !== null} onClick={() => void runTransition('receive')}>{activeAction === 'receive' ? 'Recebendo…' : 'Confirmar recebimento'}</button></>}
          {transfer.status === 'Received' && <p>A transferência foi concluída. Saída e entrada permanecem disponíveis no histórico de estoque.</p>}
          {transfer.status === 'Cancelled' && <p>A solicitação foi cancelada antes da expedição e não alterou estoque.</p>}
          {!canManage && <p>Seu perfil permite consulta, mas não transições de estoque.</p>}
        </article>
        <NavLink className="button button-secondary" to="/inventory/transfers">Voltar às transferências</NavLink>
      </aside>
    </section>
  )
}
