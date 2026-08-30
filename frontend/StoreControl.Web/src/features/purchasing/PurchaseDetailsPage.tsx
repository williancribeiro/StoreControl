import { useCallback, useEffect, useRef, useState } from 'react'
import { NavLink, useParams } from 'react-router-dom'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { getProblemMessage } from '../../shared/api/problemDetails'
import {
  cancelPurchase,
  finalizePurchase,
  getPurchase,
  receivePurchase,
  submitPurchase,
} from './purchaseService'
import { purchaseStatusLabels, type PurchaseOrderDetails } from './purchaseTypes'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function PurchaseDetailsPage() {
  const { purchaseId } = useParams()
  const id = Number(purchaseId)
  const { hasPermission } = useAuth()
  const canManage = hasPermission(permissions.purchasesManage)
  const [purchase, setPurchase] = useState<PurchaseOrderDetails | null>(null)
  const [receiptQuantities, setReceiptQuantities] = useState<Record<number, string>>({})
  const [receiptNotes, setReceiptNotes] = useState('')
  const [cancelReason, setCancelReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busyAction, setBusyAction] = useState<string | null>(null)
  const receiptAttemptId = useRef<string | null>(null)

  const load = useCallback(async (signal?: AbortSignal) => {
    if (!Number.isInteger(id) || id <= 0) throw new Error('Identificador de compra inválido.')
    const current = await getPurchase(id, signal)
    setPurchase(current)
    setReceiptQuantities(Object.fromEntries(
      current.items
        .filter((item) => item.remainingQuantity > 0)
        .map((item) => [item.id, '']),
    ))
  }, [id])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load(controller.signal).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [load])

  async function runAction(name: string, action: () => Promise<PurchaseOrderDetails>, success: string) {
    setBusyAction(name)
    setError(null)
    setMessage(null)
    try {
      const updated = await action()
      setPurchase(updated)
      setMessage(success)
    } catch (requestError) {
      setError(getProblemMessage(requestError))
    } finally {
      setBusyAction(null)
    }
  }

  async function receive() {
    if (!purchase) return
    const items = purchase.items.flatMap((item) => {
      const parsed = Number(receiptQuantities[item.id])
      return Number.isFinite(parsed) && parsed > 0
        ? [{ purchaseOrderItemId: item.id, quantity: parsed }]
        : []
    })
    if (items.length === 0) {
      setError('Informe a quantidade recebida de pelo menos um item.')
      return
    }
    if (items.some((input) => input.quantity > (purchase.items.find((item) => item.id === input.purchaseOrderItemId)?.remainingQuantity ?? 0))) {
      setError('A quantidade recebida não pode exceder a quantidade pendente.')
      return
    }

    const attemptId = receiptAttemptId.current ?? crypto.randomUUID()
    receiptAttemptId.current = attemptId
    setBusyAction('receive')
    setError(null)
    setMessage(null)
    try {
      const result = await receivePurchase(purchase.id, attemptId, {
        rowVersion: purchase.rowVersion,
        notes: receiptNotes.trim() || undefined,
        items,
      })
      receiptAttemptId.current = null
      setPurchase(result.purchase)
      setReceiptNotes('')
      setReceiptQuantities({})
      setMessage(result.wasAlreadyProcessed
        ? 'O servidor confirmou que esta tentativa já havia sido processada; o estoque não foi duplicado.'
        : 'Recebimento confirmado e estoque movimentado.')
    } catch (requestError) {
      setError(`${getProblemMessage(requestError)} A mesma chave será reutilizada ao tentar novamente.`)
    } finally {
      setBusyAction(null)
    }
  }

  if (!purchase && !error) return <section className="panel">Carregando pedido…</section>
  if (!purchase) return <section className="panel"><div className="form-error">{error}</div></section>

  const canReceive = purchase.status === 'Pending' || purchase.status === 'PartiallyReceived'
  const canCancel = purchase.status === 'Created' || purchase.status === 'Pending'

  return (
    <section className="purchase-details-layout">
      <div className="purchase-details-main">
        <article className="panel">
          <div className="section-heading">
            <div>
              <span className="eyebrow">Pedido de compra</span>
              <h2>{purchase.number}</h2>
            </div>
            <span className={`status-pill purchase-${purchase.status.toLowerCase()}`}>
              {purchaseStatusLabels[purchase.status]}
            </span>
          </div>
          <dl className="purchase-summary-grid">
            <div><dt>Loja</dt><dd>{purchase.storeCode} · {purchase.storeName}</dd></div>
            <div><dt>Fornecedor</dt><dd>{purchase.supplierCode} · {purchase.supplierName}</dd></div>
            <div><dt>Criado por</dt><dd>{purchase.createdByUserName}</dd></div>
            <div><dt>Previsão</dt><dd>{purchase.expectedDate ? new Date(`${purchase.expectedDate}T00:00:00`).toLocaleDateString('pt-BR') : 'Não informada'}</dd></div>
            <div><dt>Criação</dt><dd>{dateTime.format(new Date(purchase.createdAtUtc))}</dd></div>
            <div><dt>Total</dt><dd>{money.format(purchase.totalAmount)}</dd></div>
          </dl>
          {purchase.notes && <p className="purchase-notes">{purchase.notes}</p>}
        </article>

        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Quantidade pedida × recebida</span><h2>Itens</h2></div></div>
          <div className="table-scroll">
            <table>
              <thead><tr><th>Produto</th><th>Pedida</th><th>Recebida</th><th>Pendente</th><th>Custo</th><th>Total</th></tr></thead>
              <tbody>
                {purchase.items.map((item) => (
                  <tr key={item.id}>
                    <td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td>
                    <td>{quantity.format(item.orderedQuantity)}</td>
                    <td className="quantity-positive">{quantity.format(item.receivedQuantity)}</td>
                    <td>{quantity.format(item.remainingQuantity)}</td>
                    <td>{money.format(item.unitCost)}</td>
                    <td>{money.format(item.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>

        {canManage && canReceive && (
          <article className="panel receipt-panel">
            <span className="eyebrow">Commit transacional com movimentações</span>
            <h2>Registrar recebimento</h2>
            <p>Deixe em branco os itens que não chegaram. Recebimentos parciais permanecem pendentes.</p>
            <div className="receipt-lines">
              {purchase.items.filter((item) => item.remainingQuantity > 0).map((item) => (
                <label key={item.id}>
                  <span>{item.productCode} · {item.productName}<small>Pendente: {quantity.format(item.remainingQuantity)}</small></span>
                  <input
                    type="number"
                    min="0.001"
                    max={item.remainingQuantity}
                    step="0.001"
                    placeholder="0"
                    value={receiptQuantities[item.id] ?? ''}
                    onChange={(event) => setReceiptQuantities((current) => ({ ...current, [item.id]: event.target.value }))}
                  />
                </label>
              ))}
            </div>
            <label className="receipt-notes">
              Observações do recebimento
              <textarea value={receiptNotes} maxLength={500} onChange={(event) => setReceiptNotes(event.target.value)} />
            </label>
            <button className="button button-primary" type="button" disabled={busyAction !== null} onClick={() => void receive()}>
              {busyAction === 'receive' ? 'Confirmando…' : 'Confirmar recebimento'}
            </button>
          </article>
        )}

        <article className="panel">
          <span className="eyebrow">Documentos de entrada</span>
          <h2>Recebimentos</h2>
          {purchase.receipts.length === 0 ? <p>Nenhum recebimento registrado. O estoque não foi alterado por este pedido.</p> : (
            <div className="receipt-history">
              {purchase.receipts.map((receipt) => (
                <section id={`receipt-${receipt.id}`} key={receipt.id}>
                  <header><strong>{dateTime.format(new Date(receipt.receivedAtUtc))}</strong><small>{receipt.receivedByUserName} · {receipt.id}</small></header>
                  {receipt.notes && <p>{receipt.notes}</p>}
                  <ul>{receipt.items.map((item) => <li key={item.purchaseOrderItemId}>{item.productCode} · {item.productName}: <strong>{quantity.format(item.quantity)}</strong></li>)}</ul>
                </section>
              ))}
            </div>
          )}
        </article>
      </div>

      <aside className="purchase-details-side">
        <article className="panel purchase-actions-panel">
          <span className="eyebrow">Fluxo controlado</span>
          <h2>Ações</h2>
          {message && <div className="form-success">{message}</div>}
          {error && <div className="form-error">{error}</div>}
          {canManage && purchase.status === 'Created' && (
            <button className="button button-primary" type="button" disabled={busyAction !== null} onClick={() => void runAction(
              'submit',
              () => submitPurchase(purchase.id, purchase.rowVersion),
              'Pedido enviado para recebimento.',
            )}>Enviar pedido</button>
          )}
          {canManage && purchase.status === 'Received' && (
            <button className="button button-primary" type="button" disabled={busyAction !== null} onClick={() => void runAction(
              'finalize',
              () => finalizePurchase(purchase.id, purchase.rowVersion),
              'Pedido finalizado.',
            )}>Finalizar pedido</button>
          )}
          {canManage && canCancel && (
            <div className="cancel-purchase">
              <label>Motivo do cancelamento<input value={cancelReason} maxLength={250} onChange={(event) => setCancelReason(event.target.value)} /></label>
              <button className="button button-danger" type="button" disabled={busyAction !== null || !cancelReason.trim()} onClick={() => void runAction(
                'cancel',
                () => cancelPurchase(purchase.id, purchase.rowVersion, cancelReason),
                'Pedido cancelado sem apagar seu histórico.',
              )}>Cancelar pedido</button>
            </div>
          )}
          <NavLink className="button button-secondary" to="/purchases">Voltar ao histórico</NavLink>
        </article>

        <article className="panel purchase-timeline-panel">
          <span className="eyebrow">Rastreabilidade</span>
          <h2>Histórico de status</h2>
          <ol className="purchase-timeline">
            {purchase.statusHistory.map((history) => (
              <li key={history.id}>
                <strong>{purchaseStatusLabels[history.status]}</strong>
                <span>{history.description}</span>
                <small>{dateTime.format(new Date(history.changedAtUtc))} · {history.changedByUserName}</small>
              </li>
            ))}
          </ol>
        </article>
      </aside>
    </section>
  )
}
