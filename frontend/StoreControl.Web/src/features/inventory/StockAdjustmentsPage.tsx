import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useSearchParams } from 'react-router-dom'
import { listStores } from '../administration/adminService'
import type { StoreSummary } from '../administration/adminTypes'
import { useAuth } from '../auth/useAuth'
import { listProducts } from '../registrations/registrationService'
import type { ProductSummary } from '../registrations/registrationTypes'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { createStockAdjustment, getStockPosition } from './inventoryService'
import { movementLabels, type StockAdjustmentResult, type StockPosition } from './inventoryTypes'

type AdjustmentForm = {
  storeId: number
  productId: number
  direction: 'positive' | 'negative'
  quantity: number
  reason: string
}

const quantityFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })

export function StockAdjustmentsPage() {
  const { user } = useAuth()
  const [searchParams] = useSearchParams()
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [products, setProducts] = useState<ProductSummary[]>([])
  const [productSearch, setProductSearch] = useState('')
  const [position, setPosition] = useState<StockPosition | null>(null)
  const [lastResult, setLastResult] = useState<StockAdjustmentResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loadingPosition, setLoadingPosition] = useState(false)
  const {
    register,
    handleSubmit,
    getValues,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<AdjustmentForm>({
    defaultValues: {
      storeId: Number(searchParams.get('storeId')) || 0,
      productId: Number(searchParams.get('productId')) || 0,
      direction: 'positive',
      quantity: 1,
      reason: '',
    },
  })

  useEffect(() => {
    const controller = new AbortController()
    const initialStoreId = Number(searchParams.get('storeId'))
    const initialProductId = Number(searchParams.get('productId'))
    const assignedStores = user?.stores.map((store) => ({
      id: store.storeId,
      code: store.storeCode,
      name: store.storeName,
      isActive: true,
      rowVersion: '',
    })) ?? []
    const storeRequest = assignedStores.length > 0
      ? Promise.resolve(assignedStores)
      : listStores(controller.signal).then((items) => items.filter((store) => store.isActive))

    void Promise.all([
      storeRequest,
      listProducts(
        { page: 1, pageSize: 100, sortBy: 'name', sortDirection: 'asc' },
        controller.signal,
      ).then((result) => result.items),
      initialStoreId > 0 && initialProductId > 0
        ? getStockPosition(initialStoreId, initialProductId, controller.signal)
        : Promise.resolve(null),
    ])
      .then(([availableStores, availableProducts, initialPosition]) => {
        setStores(availableStores)
        setProducts(availableProducts)
        setPosition(initialPosition)
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [searchParams, user])

  async function searchProducts() {
    setError(null)
    try {
      const result = await listProducts({
        page: 1,
        pageSize: 100,
        search: productSearch.trim() || undefined,
        sortBy: 'name',
        sortDirection: 'asc',
      })
      setProducts(result.items)
    } catch (requestError) {
      setError(getProblemMessage(requestError))
    }
  }

  async function refreshPosition() {
    const { storeId, productId } = getValues()
    if (!storeId || !productId) {
      setError('Selecione uma loja e um produto para consultar a posição.')
      return null
    }

    setLoadingPosition(true)
    setError(null)
    try {
      const current = await getStockPosition(storeId, productId)
      setPosition(current)
      return current
    } catch (requestError) {
      setError(getProblemMessage(requestError))
      return null
    } finally {
      setLoadingPosition(false)
    }
  }

  const visibleProducts = position && !products.some((product) => product.id === position.productId)
    ? [positionAsProduct(position), ...products]
    : products

  return (
    <section className="management-layout management-layout-adjustment">
      <article className="panel form-panel adjustment-form-panel">
        <span className="eyebrow">Operação transacional</span>
        <h2>Novo ajuste</h2>
        <p>
          A quantidade nunca é editada diretamente. O ajuste grava a nova posição e sua movimentação
          no mesmo commit.
        </p>

        <form
          className="form-stack"
          onSubmit={handleSubmit(async (values) => {
            setError(null)
            setLastResult(null)
            try {
              let current = position
              if (!current || current.storeId !== values.storeId || current.productId !== values.productId) {
                current = await getStockPosition(values.storeId, values.productId)
                setPosition(current)
              }

              const result = await createStockAdjustment({ ...values, rowVersion: current.rowVersion })
              setPosition(result.position)
              setLastResult(result)
              reset({ ...values, quantity: 1, reason: '' })
            } catch (requestError) {
              setError(getProblemMessage(requestError))
            }
          })}
        >
          <label>
            Loja
            <select
              {...register('storeId', {
                valueAsNumber: true,
                min: { value: 1, message: 'Selecione a loja.' },
                onChange: () => setPosition(null),
              })}
            >
              <option value={0}>Selecione</option>
              {stores.map((store) => (
                <option value={store.id} key={store.id}>{store.code} · {store.name}</option>
              ))}
            </select>
            {errors.storeId && <small className="field-error">{errors.storeId.message}</small>}
          </label>

          <div className="product-picker">
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
          <label>
            Produto
            <select
              {...register('productId', {
                valueAsNumber: true,
                min: { value: 1, message: 'Selecione o produto.' },
                onChange: () => setPosition(null),
              })}
            >
              <option value={0}>Selecione entre os resultados</option>
              {visibleProducts.map((product) => (
                <option value={product.id} key={product.id}>
                  {product.code} · {product.name}{product.isActive ? '' : ' (inativo)'}
                </option>
              ))}
            </select>
            {errors.productId && <small className="field-error">{errors.productId.message}</small>}
          </label>

          <button
            className="button button-secondary"
            type="button"
            disabled={loadingPosition}
            onClick={() => void refreshPosition()}
          >
            {loadingPosition ? 'Consultando…' : 'Consultar saldo e versão'}
          </button>

          <div className="form-columns">
            <label>
              Direção
              <select {...register('direction')}>
                <option value="positive">Ajuste positivo</option>
                <option value="negative">Ajuste negativo</option>
              </select>
            </label>
            <label>
              Quantidade
              <input
                type="number"
                min="0.001"
                max="999999999999.999"
                step="0.001"
                {...register('quantity', {
                  valueAsNumber: true,
                  min: { value: 0.001, message: 'A quantidade deve ser maior que zero.' },
                  required: 'Informe a quantidade.',
                })}
              />
              {errors.quantity && <small className="field-error">{errors.quantity.message}</small>}
            </label>
          </div>
          <label>
            Motivo
            <textarea
              rows={4}
              placeholder="Explique por que o saldo precisa ser corrigido"
              {...register('reason', {
                required: 'Informe o motivo do ajuste.',
                maxLength: { value: 500, message: 'Use no máximo 500 caracteres.' },
              })}
            />
            {errors.reason && <small className="field-error">{errors.reason.message}</small>}
          </label>

          {error && <div className="form-error">{error}</div>}
          <button className="button button-primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Registrando…' : 'Registrar ajuste'}
          </button>
        </form>
      </article>

      <aside className="inventory-context">
        <article className="panel">
          <span className="eyebrow">Posição selecionada</span>
          <h2>{position?.productName ?? 'Consulte um produto'}</h2>
          {position ? (
            <dl className="stock-summary">
              <div><dt>Loja</dt><dd>{position.storeCode} · {position.storeName}</dd></div>
              <div><dt>Saldo atual</dt><dd>{quantityFormat.format(position.quantityOnHand)}</dd></div>
              <div><dt>Estoque mínimo</dt><dd>{quantityFormat.format(position.minimumQuantity)}</dd></div>
              <div><dt>Estado</dt><dd>{position.isBelowMinimum ? 'Baixo estoque' : 'Normal'}</dd></div>
              <div><dt>Concorrência</dt><dd>{position.rowVersion ? 'Versão carregada' : 'Nova posição'}</dd></div>
            </dl>
          ) : (
            <p>O saldo e a versão serão carregados antes da gravação.</p>
          )}
        </article>

        {lastResult && (
          <article className="panel adjustment-result">
            <span className="eyebrow">Ajuste confirmado</span>
            <h2>Movimentação #{lastResult.movement.id}</h2>
            <p>{movementLabels[lastResult.movement.type]}: <strong>
              {lastResult.movement.quantityChange > 0 ? '+' : ''}
              {quantityFormat.format(lastResult.movement.quantityChange)}
            </strong></p>
            <p>Saldo {quantityFormat.format(lastResult.movement.quantityBefore)} → {quantityFormat.format(lastResult.movement.quantityAfter)}</p>
            <small>Correlação {lastResult.movement.correlationId}</small>
          </article>
        )}
      </aside>
    </section>
  )
}

function positionAsProduct(position: StockPosition): ProductSummary {
  return {
    id: position.productId,
    code: position.productCode,
    barcode: position.productBarcode,
    name: position.productName,
    description: null,
    categoryId: 0,
    categoryCode: '',
    categoryName: '',
    costPrice: 0,
    salePrice: 0,
    isActive: position.productIsActive,
    rowVersion: '',
  }
}
