import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { useAuth } from '../auth/useAuth'
import { listProducts } from '../registrations/registrationService'
import type { ProductSummary } from '../registrations/registrationTypes'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { createStockTransfer } from './transferService'

type TransferForm = {
  originStoreId: number
  destinationStoreId: number
  notes: string
  productId: number
  quantity: number
}

type DraftItem = {
  productId: number
  productCode: string
  productName: string
  quantity: number
}

const quantityFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })

export function NewStockTransferPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [products, setProducts] = useState<ProductSummary[]>([])
  const [items, setItems] = useState<DraftItem[]>([])
  const [productSearch, setProductSearch] = useState('')
  const [operationId, setOperationId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<TransferForm>({
    defaultValues: {
      originStoreId: 0,
      destinationStoreId: 0,
      notes: '',
      productId: 0,
      quantity: 1,
    },
  })

  useEffect(() => {
    const controller = new AbortController()
    const assigned = user?.stores.map((store) => ({
      id: store.storeId,
      code: store.storeCode,
      name: store.storeName,
      isActive: true,
      rowVersion: '',
    })) ?? []
    const storesRequest = assigned.length > 0
      ? Promise.resolve(assigned)
      : listStores(controller.signal).then((allStores) => allStores.filter((store) => store.isActive))
    void Promise.all([
      storesRequest,
      listProducts(
        { page: 1, pageSize: 100, isActive: true, sortBy: 'name', sortDirection: 'asc' },
        controller.signal,
      ).then((result) => result.items),
    ])
      .then(([availableStores, availableProducts]) => {
        setStores(availableStores)
        setProducts(availableProducts)
        if (availableStores.length >= 2) {
          setValue('originStoreId', availableStores[0].id)
          setValue('destinationStoreId', availableStores[1].id)
        }
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [setValue, user])

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
    if (!product) {
      setError('Selecione um produto entre os resultados da busca.')
      return
    }
    if (!Number.isFinite(quantity) || quantity <= 0) {
      setError('A quantidade deve ser maior que zero.')
      return
    }
    if (items.some((item) => item.productId === productId)) {
      setError('O produto selecionado já está na transferência.')
      return
    }

    setItems((current) => [...current, {
      productId,
      productCode: product.code,
      productName: product.name,
      quantity,
    }])
    setValue('productId', 0)
    setValue('quantity', 1)
    setError(null)
  }

  const totalQuantity = items.reduce((sum, item) => sum + item.quantity, 0)

  return (
    <section className="purchase-editor">
      <form className="purchase-form" onSubmit={handleSubmit(async (values) => {
        if (values.originStoreId === values.destinationStoreId) {
          setError('As lojas de origem e destino devem ser diferentes.')
          return
        }
        if (items.length === 0) {
          setError('Inclua pelo menos um item na transferência.')
          return
        }
        const currentOperationId = operationId ?? crypto.randomUUID()
        setOperationId(currentOperationId)
        setError(null)
        try {
          const result = await createStockTransfer(currentOperationId, {
            originStoreId: values.originStoreId,
            destinationStoreId: values.destinationStoreId,
            notes: values.notes.trim() || undefined,
            items: items.map((item) => ({ productId: item.productId, quantity: item.quantity })),
          })
          setOperationId(null)
          navigate(`/inventory/transfers/${result.transfer.id}`, {
            state: { message: result.wasAlreadyProcessed ? 'Transferência recuperada com segurança.' : 'Transferência criada sem alterar o estoque.' },
          })
        } catch (requestError) {
          setError(getProblemMessage(requestError))
        }
      })}>
        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Planejamento sem efeito no estoque</span><h2>Nova transferência</h2></div><span className="phase-badge">Status inicial · Criada</span></div>
          <div className="form-columns purchase-header-fields">
            <label>Loja de origem<select {...register('originStoreId', { valueAsNumber: true, min: { value: 1, message: 'Selecione a origem.' } })}><option value={0}>Selecione</option>{stores.map((store) => <option value={store.id} key={store.id}>{store.code} · {store.name}</option>)}</select>{errors.originStoreId && <small className="field-error">{errors.originStoreId.message}</small>}</label>
            <label>Loja de destino<select {...register('destinationStoreId', { valueAsNumber: true, min: { value: 1, message: 'Selecione o destino.' } })}><option value={0}>Selecione</option>{stores.map((store) => <option value={store.id} key={store.id}>{store.code} · {store.name}</option>)}</select>{errors.destinationStoreId && <small className="field-error">{errors.destinationStoreId.message}</small>}</label>
            <label>Observações<input maxLength={1000} placeholder="Motivo ou referência interna" {...register('notes')} /></label>
          </div>
          {stores.length < 2 && <div className="form-error page-message">Seu usuário precisa ter acesso a pelo menos duas lojas para criar uma transferência.</div>}
        </article>

        <article className="panel">
          <div className="section-heading"><div><span className="eyebrow">Mercadoria planejada</span><h2>Itens</h2></div><strong>{quantityFormat.format(totalQuantity)} unidades</strong></div>
          <div className="product-picker purchase-product-search"><label>Buscar produto<input value={productSearch} placeholder="Código, barras ou nome" onChange={(event) => setProductSearch(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); void searchProducts() } }} /></label><button className="button button-secondary" type="button" onClick={() => void searchProducts()}>Buscar</button></div>
          <div className="purchase-item-inputs"><label>Produto<select {...register('productId', { valueAsNumber: true })}><option value={0}>Selecione entre os resultados</option>{products.map((product) => <option value={product.id} key={product.id}>{product.code} · {product.name}</option>)}</select></label><label>Quantidade<input type="number" min="0.001" step="0.001" {...register('quantity', { valueAsNumber: true })} /></label><button className="button button-secondary" type="button" onClick={addItem}>Adicionar</button></div>
          <div className="table-scroll purchase-items-table"><table><thead><tr><th>Produto</th><th>Quantidade</th><th /></tr></thead><tbody>{items.map((item) => <tr key={item.productId}><td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td><td>{quantityFormat.format(item.quantity)}</td><td className="table-actions"><button className="link-button danger-text" type="button" onClick={() => setItems((current) => current.filter((candidate) => candidate.productId !== item.productId))}>Remover</button></td></tr>)}{items.length === 0 && <tr><td colSpan={3}>Nenhum item incluído.</td></tr>}</tbody></table></div>
        </article>

        {error && <div className="form-error page-message">{error}</div>}
        <div className="purchase-submit-row"><p>A criação apenas registra a solicitação. O estoque sairá na expedição.</p><button className="button button-primary" type="submit" disabled={isSubmitting || stores.length < 2}>{isSubmitting ? 'Salvando…' : 'Criar transferência'}</button></div>
      </form>
    </section>
  )
}
