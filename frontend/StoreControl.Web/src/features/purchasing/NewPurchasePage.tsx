import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { useAuth } from '../auth/useAuth'
import { listProducts, listSuppliers } from '../registrations/registrationService'
import type { PartySummary, ProductSummary } from '../registrations/registrationTypes'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { createPurchase } from './purchaseService'

type PurchaseForm = {
  storeId: number
  supplierId: number
  expectedDate: string
  notes: string
  productId: number
  quantity: number
  unitCost: number
}

type DraftItem = {
  productId: number
  productCode: string
  productName: string
  quantity: number
  unitCost: number
}

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const quantityFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })

export function NewPurchasePage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [suppliers, setSuppliers] = useState<PartySummary[]>([])
  const [products, setProducts] = useState<ProductSummary[]>([])
  const [items, setItems] = useState<DraftItem[]>([])
  const [productSearch, setProductSearch] = useState('')
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<PurchaseForm>({
    defaultValues: {
      storeId: 0,
      supplierId: 0,
      expectedDate: '',
      notes: '',
      productId: 0,
      quantity: 1,
      unitCost: 0,
    },
  })

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
      : listStores(controller.signal).then((allStores) => allStores.filter((store) => store.isActive))

    void Promise.all([
      storesRequest,
      listSuppliers(
        { page: 1, pageSize: 100, isActive: true, sortBy: 'name', sortDirection: 'asc' },
        controller.signal,
      ).then((result) => result.items),
      listProducts(
        { page: 1, pageSize: 100, isActive: true, sortBy: 'name', sortDirection: 'asc' },
        controller.signal,
      ).then((result) => result.items),
    ])
      .then(([availableStores, availableSuppliers, availableProducts]) => {
        setStores(availableStores)
        setSuppliers(availableSuppliers)
        setProducts(availableProducts)
        if (availableStores.length === 1) setValue('storeId', availableStores[0].id)
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
    const { productId, quantity, unitCost } = getValues()
    const product = products.find((candidate) => candidate.id === productId)
    if (!product) {
      setError('Selecione um produto entre os resultados da busca.')
      return
    }
    if (!Number.isFinite(quantity) || quantity <= 0) {
      setError('A quantidade do item deve ser maior que zero.')
      return
    }
    if (!Number.isFinite(unitCost) || unitCost < 0) {
      setError('O custo unitário não pode ser negativo.')
      return
    }
    if (items.some((item) => item.productId === productId)) {
      setError('O produto selecionado já está no pedido.')
      return
    }

    setItems((current) => [...current, {
      productId,
      productCode: product.code,
      productName: product.name,
      quantity,
      unitCost,
    }])
    setValue('productId', 0)
    setValue('quantity', 1)
    setValue('unitCost', 0)
    setError(null)
  }

  const total = items.reduce((sum, item) => sum + item.quantity * item.unitCost, 0)

  return (
    <section className="purchase-editor">
      <form
        className="purchase-form"
        onSubmit={handleSubmit(async (values) => {
          if (items.length === 0) {
            setError('Inclua pelo menos um item no pedido.')
            return
          }
          setError(null)
          try {
            const created = await createPurchase({
              storeId: values.storeId,
              supplierId: values.supplierId,
              expectedDate: values.expectedDate || undefined,
              notes: values.notes.trim() || undefined,
              items: items.map((item) => ({
                productId: item.productId,
                quantity: item.quantity,
                unitCost: item.unitCost,
              })),
            })
            navigate(`/purchases/${created.id}`)
          } catch (requestError) {
            setError(getProblemMessage(requestError))
          }
        })}
      >
        <article className="panel">
          <div className="section-heading">
            <div>
              <span className="eyebrow">Compra sem efeito imediato no estoque</span>
              <h2>Dados do pedido</h2>
            </div>
            <span className="phase-badge">Status inicial · Criada</span>
          </div>
          <div className="form-columns purchase-header-fields">
            <label>
              Loja
              <select {...register('storeId', {
                valueAsNumber: true,
                min: { value: 1, message: 'Selecione a loja.' },
              })}>
                <option value={0}>Selecione</option>
                {stores.map((store) => (
                  <option value={store.id} key={store.id}>{store.code} · {store.name}</option>
                ))}
              </select>
              {errors.storeId && <small className="field-error">{errors.storeId.message}</small>}
            </label>
            <label>
              Fornecedor
              <select {...register('supplierId', {
                valueAsNumber: true,
                min: { value: 1, message: 'Selecione o fornecedor.' },
              })}>
                <option value={0}>Selecione</option>
                {suppliers.map((supplier) => (
                  <option value={supplier.id} key={supplier.id}>{supplier.code} · {supplier.name}</option>
                ))}
              </select>
              {errors.supplierId && <small className="field-error">{errors.supplierId.message}</small>}
            </label>
            <label>
              Previsão de entrega
              <input type="date" {...register('expectedDate')} />
            </label>
            <label>
              Observações
              <input maxLength={1000} placeholder="Condições ou referência interna" {...register('notes')} />
            </label>
          </div>
        </article>

        <article className="panel">
          <div className="section-heading">
            <div>
              <span className="eyebrow">Preços históricos</span>
              <h2>Itens da compra</h2>
            </div>
            <strong>{money.format(total)}</strong>
          </div>
          <div className="product-picker purchase-product-search">
            <label>
              Buscar produto
              <input
                value={productSearch}
                placeholder="Código, barras ou nome"
                onChange={(event) => setProductSearch(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault()
                    void searchProducts()
                  }
                }}
              />
            </label>
            <button className="button button-secondary" type="button" onClick={() => void searchProducts()}>
              Buscar
            </button>
          </div>
          <div className="purchase-item-inputs">
            <label>
              Produto
              <select {...register('productId', {
                valueAsNumber: true,
                onChange: (event) => {
                  const product = products.find((item) => item.id === Number(event.target.value))
                  setValue('unitCost', product?.costPrice ?? 0)
                },
              })}>
                <option value={0}>Selecione entre os resultados</option>
                {products.map((product) => (
                  <option value={product.id} key={product.id}>{product.code} · {product.name}</option>
                ))}
              </select>
            </label>
            <label>
              Quantidade
              <input type="number" min="0.001" step="0.001" {...register('quantity', { valueAsNumber: true })} />
            </label>
            <label>
              Custo unitário
              <input type="number" min="0" step="0.0001" {...register('unitCost', { valueAsNumber: true })} />
            </label>
            <button className="button button-secondary" type="button" onClick={addItem}>Adicionar item</button>
          </div>

          <div className="table-scroll purchase-items-table">
            <table>
              <thead><tr><th>Produto</th><th>Quantidade</th><th>Custo unitário</th><th>Total</th><th /></tr></thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.productId}>
                    <td><strong>{item.productName}</strong><small className="table-subtitle">{item.productCode}</small></td>
                    <td>{quantityFormat.format(item.quantity)}</td>
                    <td>{money.format(item.unitCost)}</td>
                    <td>{money.format(item.quantity * item.unitCost)}</td>
                    <td className="table-actions">
                      <button className="link-button" type="button" onClick={() => setItems((current) => current.filter((candidate) => candidate.productId !== item.productId))}>
                        Remover
                      </button>
                    </td>
                  </tr>
                ))}
                {items.length === 0 && <tr><td colSpan={5}>Nenhum item incluído.</td></tr>}
              </tbody>
            </table>
          </div>
          {error && <div className="form-error page-message">{error}</div>}
          <div className="purchase-submit-row">
            <p>O saldo somente mudará quando um recebimento for confirmado.</p>
            <button className="button button-primary" type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Criando…' : 'Criar pedido'}
            </button>
          </div>
        </article>
      </form>
    </section>
  )
}
