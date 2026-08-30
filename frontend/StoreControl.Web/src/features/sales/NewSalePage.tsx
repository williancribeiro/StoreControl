import { useEffect, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { NavLink, useNavigate } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { useAuth } from '../auth/useAuth'
import { getCurrentCashSession, listCashRegisters } from '../cash/cashService'
import type { CashRegister, CashSessionDetails } from '../cash/cashTypes'
import { listCustomers, listProducts } from '../registrations/registrationService'
import type { PartySummary, ProductSummary } from '../registrations/registrationTypes'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { finalizeSale } from './salesService'
import { paymentMethodLabels, paymentMethods, type PaymentMethod } from './salesTypes'

type SaleHeaderForm = {
  storeId: number
  customerId: number
  discountAmount: number
  productId: number
  quantity: number
}

type CartItem = Pick<ProductSummary, 'id' | 'code' | 'name' | 'salePrice'> & { quantity: number }
type DraftPayment = { key: string; method: PaymentMethod; amount: number; reference: string }

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const roundMoney = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100

export function NewSalePage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [operationId] = useState(() => crypto.randomUUID())
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [registers, setRegisters] = useState<CashRegister[]>([])
  const [cashRegisterId, setCashRegisterId] = useState(0)
  const [cashSession, setCashSession] = useState<CashSessionDetails | undefined>()
  const [customers, setCustomers] = useState<PartySummary[]>([])
  const [products, setProducts] = useState<ProductSummary[]>([])
  const [items, setItems] = useState<CartItem[]>([])
  const [payments, setPayments] = useState<DraftPayment[]>([
    { key: crypto.randomUUID(), method: 'Cash', amount: 0, reference: '' },
  ])
  const [productSearch, setProductSearch] = useState('')
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = useForm<SaleHeaderForm>({
    defaultValues: { storeId: 0, customerId: 0, discountAmount: 0, productId: 0, quantity: 1 },
  })
  const storeId = useWatch({ control, name: 'storeId' })
  const discount = useWatch({ control, name: 'discountAmount' }) || 0

  useEffect(() => {
    const controller = new AbortController()
    const assignedStores = user?.stores.map((store) => ({
      id: store.storeId,
      code: store.storeCode,
      name: store.storeName,
      isActive: true,
      rowVersion: '',
    })) ?? []
    const storesRequest = assignedStores.length > 0
      ? Promise.resolve(assignedStores)
      : listStores(controller.signal).then((all) => all.filter((store) => store.isActive))

    void Promise.all([
      storesRequest,
      listCustomers(
        { page: 1, pageSize: 100, isActive: true, sortBy: 'name', sortDirection: 'asc' },
        controller.signal,
      ).then((result) => result.items),
      listProducts(
        { page: 1, pageSize: 100, isActive: true, sortBy: 'name', sortDirection: 'asc' },
        controller.signal,
      ).then((result) => result.items),
    ]).then(([availableStores, availableCustomers, availableProducts]) => {
      setStores(availableStores)
      setCustomers(availableCustomers)
      setProducts(availableProducts)
      if (availableStores.length === 1) setValue('storeId', availableStores[0].id)
    }).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [setValue, user])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    setRegisters([])
    setCashRegisterId(0)
    setCashSession(undefined)
    if (!storeId) return () => controller.abort()

    void listCashRegisters(storeId, controller.signal)
      .then((available) => {
        setRegisters(available)
        if (available.length > 0) setCashRegisterId(available[0].id)
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [storeId])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    setCashSession(undefined)
    if (!cashRegisterId) return () => controller.abort()
    void getCurrentCashSession(cashRegisterId, controller.signal)
      .then(setCashSession)
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [cashRegisterId])

  async function searchProducts() {
    setError(null)
    try {
      const result = await listProducts({
        page: 1,
        pageSize: 100,
        search: productSearch.trim() || undefined,
        isActive: true,
        sortBy: 'name',
        sortDirection: 'asc',
      })
      setProducts(result.items)
    } catch (requestError) {
      setError(getProblemMessage(requestError))
    }
  }

  function addItem() {
    const { productId, quantity } = getValues()
    const product = products.find((candidate) => candidate.id === productId)
    if (!product) return setError('Selecione um produto entre os resultados da busca.')
    if (!Number.isFinite(quantity) || quantity <= 0) return setError('A quantidade deve ser maior que zero.')
    if (items.some((item) => item.id === product.id)) return setError('O produto já está no carrinho.')
    setItems((current) => [...current, {
      id: product.id,
      code: product.code,
      name: product.name,
      salePrice: product.salePrice,
      quantity,
    }])
    setValue('productId', 0)
    setValue('quantity', 1)
    setError(null)
  }

  const subtotal = roundMoney(items.reduce((sum, item) => sum + roundMoney(item.salePrice * item.quantity), 0))
  const total = roundMoney(subtotal - discount)
  const paid = roundMoney(payments.reduce((sum, payment) => sum + (Number(payment.amount) || 0), 0))
  const remaining = roundMoney(total - paid)

  function updatePayment(key: string, change: Partial<DraftPayment>) {
    setPayments((current) => current.map((payment) => payment.key === key
      ? { ...payment, ...change }
      : payment))
  }

  return (
    <section className="pos-layout">
      <form id="sale-form" className="pos-main" onSubmit={handleSubmit(async (values) => {
        if (!cashSession) return setError('Abra o caixa selecionado antes de finalizar a venda.')
        if (items.length === 0) return setError('Inclua pelo menos um produto no carrinho.')
        if (items.some((item) => !Number.isFinite(item.quantity) || item.quantity <= 0)) {
          return setError('Todas as quantidades do carrinho devem ser maiores que zero.')
        }
        if (values.discountAmount < 0 || values.discountAmount >= subtotal) {
          return setError('O desconto deve ser menor que o subtotal da venda.')
        }
        if (payments.some((payment) => payment.amount <= 0)) {
          return setError('Todos os pagamentos devem possuir valor maior que zero.')
        }
        if (remaining !== 0) return setError('A soma dos pagamentos deve ser exatamente igual ao total.')

        setError(null)
        try {
          const result = await finalizeSale(operationId, {
            storeId: values.storeId,
            cashSessionId: cashSession.id,
            customerId: values.customerId || undefined,
            discountAmount: values.discountAmount,
            items: items.map((item) => ({ productId: item.id, quantity: item.quantity })),
            payments: payments.map((payment) => ({
              method: payment.method,
              amount: payment.amount,
              reference: payment.reference.trim() || undefined,
            })),
          })
          navigate(`/sales/${result.sale.id}`, {
            state: { message: result.wasAlreadyProcessed ? 'Venda já processada confirmada.' : 'Venda finalizada.' },
          })
        } catch (requestError) {
          setError(`${getProblemMessage(requestError)} A chave desta tentativa será reutilizada com segurança.`)
        }
      })}>
        <article className="panel pos-header">
          <div className="section-heading">
            <div><span className="eyebrow">Ponto de venda</span><h2>Contexto da operação</h2></div>
            <span className={cashSession ? 'status-pill purchase-finalized' : 'status-pill purchase-cancelled'}>
              {cashSession ? `Caixa aberto · ${cashSession.cashRegisterCode}` : 'Caixa fechado'}
            </span>
          </div>
          <div className="form-columns pos-context-fields">
            <label>
              Loja
              <select {...register('storeId', { valueAsNumber: true, min: { value: 1, message: 'Selecione a loja.' } })}>
                <option value={0}>Selecione</option>
                {stores.map((store) => <option key={store.id} value={store.id}>{store.code} · {store.name}</option>)}
              </select>
              {errors.storeId && <small className="field-error">{errors.storeId.message}</small>}
            </label>
            <label>
              Caixa
              <select value={cashRegisterId} onChange={(event) => setCashRegisterId(Number(event.target.value))}>
                <option value={0}>Selecione</option>
                {registers.map((cashRegister) => <option key={cashRegister.id} value={cashRegister.id}>{cashRegister.code} · {cashRegister.name}</option>)}
              </select>
            </label>
            <label>
              Cliente opcional
              <select {...register('customerId', { valueAsNumber: true })}>
                <option value={0}>Consumidor não identificado</option>
                {customers.map((customer) => <option key={customer.id} value={customer.id}>{customer.code} · {customer.name}</option>)}
              </select>
            </label>
          </div>
          {!cashSession && cashRegisterId > 0 && (
            <p className="cash-warning">Este caixa não possui sessão aberta. <NavLink to="/cash/current">Abrir operação de caixa</NavLink>.</p>
          )}
        </article>

        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Busca por código, barras ou nome</span><h2>Produtos</h2></div></div>
          <div className="product-picker pos-product-search">
            <label>
              Buscar produto
              <input value={productSearch} onChange={(event) => setProductSearch(event.target.value)} onKeyDown={(event) => {
                if (event.key === 'Enter') { event.preventDefault(); void searchProducts() }
              }} />
            </label>
            <button className="button button-secondary" type="button" onClick={() => void searchProducts()}>Buscar</button>
          </div>
          <div className="pos-item-inputs">
            <label>
              Produto
              <select {...register('productId', { valueAsNumber: true })}>
                <option value={0}>Selecione entre os resultados</option>
                {products.map((product) => <option key={product.id} value={product.id}>{product.code} · {product.name} · {money.format(product.salePrice)}</option>)}
              </select>
            </label>
            <label>
              Quantidade
              <input type="number" min="0.001" step="0.001" {...register('quantity', { valueAsNumber: true })} />
            </label>
            <button className="button button-primary" type="button" onClick={addItem}>Adicionar ao carrinho</button>
          </div>
        </article>

        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Preço capturado na finalização</span><h2>Carrinho</h2></div><strong>{items.length} item(ns)</strong></div>
          <div className="table-scroll">
            <table>
              <thead><tr><th>Produto</th><th>Quantidade</th><th>Preço</th><th>Subtotal</th><th /></tr></thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id}>
                    <td><strong>{item.name}</strong><small className="table-subtitle">{item.code}</small></td>
                    <td><input className="table-number-input" type="number" min="0.001" step="0.001" value={item.quantity} onChange={(event) => {
                      const quantity = Number(event.target.value)
                      setItems((current) => current.map((candidate) => candidate.id === item.id ? { ...candidate, quantity } : candidate))
                    }} /></td>
                    <td>{money.format(item.salePrice)}</td>
                    <td>{money.format(roundMoney(item.salePrice * item.quantity))}</td>
                    <td className="table-actions"><button className="link-button" type="button" onClick={() => setItems((current) => current.filter((candidate) => candidate.id !== item.id))}>Remover</button></td>
                  </tr>
                ))}
                {items.length === 0 && <tr><td colSpan={5}>Carrinho vazio.</td></tr>}
              </tbody>
            </table>
          </div>
        </article>
      </form>

      <aside className="pos-sidebar">
        <article className="panel pos-totals">
          <span className="eyebrow">Fechamento da venda</span>
          <h2>Totais e pagamentos</h2>
          <dl>
            <div><dt>Subtotal</dt><dd>{money.format(subtotal)}</dd></div>
            <div><dt>Desconto</dt><dd><input form="sale-form" type="number" min="0" max={Math.max(0, subtotal - 0.01)} step="0.01" {...register('discountAmount', { valueAsNumber: true })} /></dd></div>
            <div className="pos-total-line"><dt>Total</dt><dd>{money.format(total)}</dd></div>
          </dl>
          <div className="payment-lines">
            {payments.map((payment, index) => (
              <section key={payment.key}>
                <select value={payment.method} onChange={(event) => updatePayment(payment.key, { method: event.target.value as PaymentMethod })}>
                  {paymentMethods.map((method) => <option value={method} key={method}>{paymentMethodLabels[method]}</option>)}
                </select>
                <input aria-label={`Valor do pagamento ${index + 1}`} type="number" min="0.01" step="0.01" value={payment.amount} onChange={(event) => updatePayment(payment.key, { amount: Number(event.target.value) })} />
                <input aria-label={`Referência do pagamento ${index + 1}`} placeholder="Referência opcional" maxLength={100} value={payment.reference} onChange={(event) => updatePayment(payment.key, { reference: event.target.value })} />
                {payments.length > 1 && <button className="link-button" type="button" onClick={() => setPayments((current) => current.filter((candidate) => candidate.key !== payment.key))}>Remover</button>}
              </section>
            ))}
          </div>
          <div className="payment-actions">
            <button className="link-button" type="button" onClick={() => setPayments((current) => [...current, { key: crypto.randomUUID(), method: 'Pix', amount: 0, reference: '' }])}>+ Outra forma</button>
            <button className="link-button" type="button" onClick={() => {
              if (payments.length > 0) updatePayment(payments[payments.length - 1].key, { amount: roundMoney(payments[payments.length - 1].amount + remaining) })
            }}>Preencher saldo</button>
          </div>
          <div className={remaining === 0 ? 'payment-balance balanced' : 'payment-balance'}>
            <span>Pago {money.format(paid)}</span><strong>{remaining === 0 ? 'Valores conferem' : `Falta ${money.format(remaining)}`}</strong>
          </div>
          {error && <div className="form-error">{error}</div>}
          <button className="button button-primary pos-finalize" form="sale-form" type="submit" disabled={isSubmitting || !cashSession || items.length === 0}>
            {isSubmitting ? 'Finalizando…' : 'Finalizar venda'}
          </button>
          <small className="pos-transaction-note">Estoque, venda, pagamentos e caixa serão confirmados na mesma transação.</small>
        </article>
      </aside>
    </section>
  )
}
