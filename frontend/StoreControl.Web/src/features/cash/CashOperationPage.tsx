import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { NavLink } from 'react-router-dom'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { paymentMethodLabels } from '../sales/salesTypes'
import { addCashMovement, closeCashSession, getCurrentCashSession, listCashRegisters, openCashSession } from './cashService'
import type { CashRegister, CashSessionDetails } from './cashTypes'

type OpenForm = { openingAmount: number }
type MovementForm = { type: 'CashIn' | 'CashOut'; amount: number; reason: string }
type CloseForm = { declaredClosingAmount: number }

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function CashOperationPage() {
  const [registers, setRegisters] = useState<CashRegister[]>([])
  const [cashRegisterId, setCashRegisterId] = useState(0)
  const [session, setSession] = useState<CashSessionDetails | undefined>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const openForm = useForm<OpenForm>({ defaultValues: { openingAmount: 0 } })
  const movementForm = useForm<MovementForm>({ defaultValues: { type: 'CashIn', amount: 0, reason: '' } })
  const closeForm = useForm<CloseForm>({ defaultValues: { declaredClosingAmount: 0 } })
  const setDeclaredClosingAmount = closeForm.setValue

  const loadCurrent = useCallback(async (registerId: number, signal?: AbortSignal) => {
    setLoading(true)
    try {
      const current = await getCurrentCashSession(registerId, signal)
      setSession(current)
      setDeclaredClosingAmount('declaredClosingAmount', current?.expectedCashAmount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [setDeclaredClosingAmount])

  useEffect(() => {
    const controller = new AbortController()
    void listCashRegisters(undefined, controller.signal).then((available) => {
      setRegisters(available)
      if (available.length > 0) setCashRegisterId(available[0].id)
    }).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    if (!cashRegisterId) {
      // oxlint-disable-next-line react/set-state-in-effect
      setLoading(false)
      return () => controller.abort()
    }
    // oxlint-disable-next-line react/set-state-in-effect
    void loadCurrent(cashRegisterId, controller.signal).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [cashRegisterId, loadCurrent])

  async function run(action: () => Promise<CashSessionDetails>, success: string) {
    setError(null)
    setMessage(null)
    try {
      const updated = await action()
      setSession(updated)
      setDeclaredClosingAmount('declaredClosingAmount', updated.expectedCashAmount)
      setMessage(success)
      return true
    } catch (requestError) {
      setError(getProblemMessage(requestError))
      return false
    }
  }

  const selectedRegister = registers.find((item) => item.id === cashRegisterId)

  return (
    <section className="cash-operation-layout">
      <div className="cash-operation-main">
        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Terminal e loja</span><h2>Operação atual</h2></div>{session && <span className="status-pill purchase-finalized">Aberto</span>}</div>
          <label className="cash-register-select">Caixa<select value={cashRegisterId} onChange={(event) => { setCashRegisterId(Number(event.target.value)); setError(null); setMessage(null) }}><option value={0}>Selecione</option>{registers.map((cashRegister) => <option value={cashRegister.id} key={cashRegister.id}>{cashRegister.storeCode} · {cashRegister.code} · {cashRegister.name}</option>)}</select></label>
        </article>

        {loading && <article className="panel">Consultando sessão atual…</article>}
        {!loading && cashRegisterId > 0 && !session && (
          <article className="panel cash-open-panel">
            <span className="eyebrow">Início do turno</span><h2>Abrir caixa</h2>
            <p>Informe apenas o dinheiro físico disponível para troco. Cartões e Pix serão registrados no ledger, mas não aumentam o saldo físico esperado.</p>
            <form onSubmit={openForm.handleSubmit(async (values) => {
              setError(null)
              try {
                const result = await openCashSession(crypto.randomUUID(), cashRegisterId, values.openingAmount)
                setSession(result.session)
                setDeclaredClosingAmount('declaredClosingAmount', result.session.expectedCashAmount)
                setMessage(result.wasAlreadyProcessed ? 'A abertura já havia sido processada.' : 'Caixa aberto com sucesso.')
              } catch (requestError) { setError(getProblemMessage(requestError)) }
            })}>
              <label>Valor de abertura<input type="number" min="0" step="0.01" {...openForm.register('openingAmount', { valueAsNumber: true, min: 0 })} /></label>
              <button className="button button-primary" type="submit" disabled={openForm.formState.isSubmitting}>{openForm.formState.isSubmitting ? 'Abrindo…' : 'Abrir caixa'}</button>
            </form>
          </article>
        )}

        {session && (
          <>
            <article className="panel">
              <div className="section-heading"><div><span className="eyebrow">Ledger assinado</span><h2>Movimentações</h2></div><strong>{session.movements.length} lançamento(s)</strong></div>
              <div className="table-scroll"><table><thead><tr><th>Data</th><th>Tipo</th><th>Forma</th><th>Motivo</th><th>Operador</th><th>Valor</th></tr></thead><tbody>
                {session.movements.map((movement) => <tr key={movement.id}><td>{dateTime.format(new Date(movement.occurredAtUtc))}</td><td>{movementTypeLabel(movement.type)}</td><td>{movement.paymentMethod ? paymentMethodLabels[movement.paymentMethod] : '—'}</td><td>{movement.reason}{movement.referenceId && <small className="table-subtitle">{movement.referenceType} · {movement.referenceId}</small>}</td><td>{movement.createdByUserName}</td><td className={movement.amount < 0 ? 'quantity-negative' : 'quantity-positive'}>{money.format(movement.amount)}</td></tr>)}
                {session.movements.length === 0 && <tr><td colSpan={6}>Nenhuma movimentação nesta sessão.</td></tr>}
              </tbody></table></div>
            </article>

            <article className="panel cash-manual-panel">
              <span className="eyebrow">Entrada ou saída rastreável</span><h2>Movimentação manual</h2>
              <form onSubmit={movementForm.handleSubmit(async (values) => {
                const succeeded = await run(async () => {
                  const result = await addCashMovement(session.id, crypto.randomUUID(), {
                    rowVersion: session.rowVersion,
                    type: values.type,
                    amount: values.amount,
                    reason: values.reason.trim(),
                  })
                  return result.session
                }, values.type === 'CashIn' ? 'Entrada registrada.' : 'Saída registrada.')
                if (succeeded) movementForm.reset({ type: values.type, amount: 0, reason: '' })
              })}>
                <label>Tipo<select {...movementForm.register('type')}><option value="CashIn">Entrada</option><option value="CashOut">Saída</option></select></label>
                <label>Valor<input type="number" min="0.01" step="0.01" {...movementForm.register('amount', { valueAsNumber: true, min: 0.01 })} /></label>
                <label>Motivo<input maxLength={250} {...movementForm.register('reason', { required: true })} /></label>
                <button className="button button-secondary" type="submit" disabled={movementForm.formState.isSubmitting}>Registrar</button>
              </form>
            </article>
          </>
        )}
      </div>

      <aside className="cash-operation-side">
        <article className="panel cash-summary-card">
          <span className="eyebrow">Conferência</span><h2>{selectedRegister?.code ?? 'Caixa'}</h2>
          {session ? <>
            <dl><div><dt>Abertura</dt><dd>{money.format(session.openingAmount)}</dd></div><div><dt>Dinheiro esperado</dt><dd>{money.format(session.expectedCashAmount)}</dd></div><div><dt>Aberto por</dt><dd>{session.openedByUserName}</dd></div><div><dt>Desde</dt><dd>{dateTime.format(new Date(session.openedAtUtc))}</dd></div></dl>
            <form className="cash-close-form" onSubmit={closeForm.handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              try {
                await closeCashSession(session.id, session.rowVersion, values.declaredClosingAmount)
                setSession(undefined)
                setMessage('Caixa fechado e diferença apurada. Consulte o histórico para a conferência final.')
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              }
            })}>
              <label>Dinheiro contado<input type="number" min="0" step="0.01" {...closeForm.register('declaredClosingAmount', { valueAsNumber: true, min: 0 })} /></label>
              <button className="button button-danger" type="submit" disabled={closeForm.formState.isSubmitting}>Fechar caixa</button>
            </form>
          </> : <p>Nenhuma sessão aberta para o caixa selecionado.</p>}
          {message && <div className="form-success">{message}</div>}
          {error && <div className="form-error">{error}</div>}
          <NavLink className="button button-secondary" to="/cash/history">Ver histórico</NavLink>
        </article>
      </aside>
    </section>
  )
}

function movementTypeLabel(type: string) {
  return ({ Opening: 'Abertura', SalePayment: 'Venda', CashIn: 'Entrada', CashOut: 'Saída' } as Record<string, string>)[type] ?? type
}
