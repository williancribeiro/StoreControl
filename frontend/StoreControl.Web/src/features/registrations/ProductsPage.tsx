import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from './Pagination'
import { createProduct, listCategories, listProducts, updateProduct } from './registrationService'
import type {
  CategorySummary,
  PagedResult,
  ProductListQuery,
  ProductSummary,
} from './registrationTypes'

type ProductForm = {
  code: string
  barcode: string
  name: string
  description: string
  categoryId: number
  costPrice: number
  salePrice: number
  isActive: boolean
}

const initialQuery: ProductListQuery = {
  page: 1,
  pageSize: 10,
  sortBy: 'name',
  sortDirection: 'asc',
}

const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function ProductsPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(permissions.productsManage)
  const [result, setResult] = useState<PagedResult<ProductSummary> | null>(null)
  const [categories, setCategories] = useState<CategorySummary[]>([])
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [categoryFilter, setCategoryFilter] = useState('all')
  const [editing, setEditing] = useState<ProductSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ProductForm>({
    defaultValues: {
      code: '',
      barcode: '',
      name: '',
      description: '',
      categoryId: 0,
      costPrice: 0,
      salePrice: 0,
      isActive: true,
    },
  })

  const load = useCallback(async (activeQuery: ProductListQuery, signal?: AbortSignal) => {
    setResult(await listProducts(activeQuery, signal))
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load(query, controller.signal).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [load, query])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void listCategories(
      { page: 1, pageSize: 100, sortBy: 'name', sortDirection: 'asc' },
      controller.signal,
    )
      .then((response) => setCategories(response.items))
      .catch((requestError) => {
        if (!controller.signal.aborted) setError(getProblemMessage(requestError))
      })
    return () => controller.abort()
  }, [])

  function emptyForm(): ProductForm {
    return {
      code: '',
      barcode: '',
      name: '',
      description: '',
      categoryId: categories.find((category) => category.isActive)?.id ?? 0,
      costPrice: 0,
      salePrice: 0,
      isActive: true,
    }
  }

  function clearForm() {
    setEditing(null)
    reset(emptyForm())
  }

  function startEditing(product: ProductSummary) {
    setEditing(product)
    reset({
      code: product.code,
      barcode: product.barcode ?? '',
      name: product.name,
      description: product.description ?? '',
      categoryId: product.categoryId,
      costPrice: product.costPrice,
      salePrice: product.salePrice,
      isActive: product.isActive,
    })
    setMessage(null)
    setError(null)
  }

  function applyFilters() {
    setQuery((current) => ({
      ...current,
      page: 1,
      search: search.trim() || undefined,
      isActive: status === 'all' ? undefined : status === 'active',
      categoryId: categoryFilter === 'all' ? undefined : Number(categoryFilter),
    }))
  }

  return (
    <section className="management-layout management-layout-products">
      <article className="panel table-panel">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Cadastros</span>
            <h2>Produtos</h2>
          </div>
          <span className="count-badge">{result?.totalCount ?? 0} registros</span>
        </div>
        <div className="filter-grid">
          <label>
            Busca
            <input
              value={search}
              placeholder="Código, barras, nome ou descrição"
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && applyFilters()}
            />
          </label>
          <label>
            Categoria
            <select value={categoryFilter} onChange={(event) => setCategoryFilter(event.target.value)}>
              <option value="all">Todas</option>
              {categories.map((category) => (
                <option value={category.id} key={category.id}>{category.name}</option>
              ))}
            </select>
          </label>
          <label>
            Status
            <select value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="all">Todos</option>
              <option value="active">Ativos</option>
              <option value="inactive">Inativos</option>
            </select>
          </label>
          <label>
            Ordenar
            <select
              value={`${query.sortBy}:${query.sortDirection}`}
              onChange={(event) => {
                const [sortBy, sortDirection] = event.target.value.split(':')
                setQuery((current) => ({
                  ...current,
                  page: 1,
                  sortBy,
                  sortDirection: sortDirection as 'asc' | 'desc',
                }))
              }}
            >
              <option value="name:asc">Nome A–Z</option>
              <option value="name:desc">Nome Z–A</option>
              <option value="code:asc">Código crescente</option>
              <option value="category:asc">Categoria</option>
              <option value="salePrice:asc">Menor preço de venda</option>
              <option value="salePrice:desc">Maior preço de venda</option>
            </select>
          </label>
          <button className="button button-secondary filter-action" type="button" onClick={applyFilters}>
            Aplicar
          </button>
        </div>
        {error && <div className="form-error page-message">{error}</div>}
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Produto</th>
                <th>Categoria</th>
                <th>Preços</th>
                <th>Status</th>
                {canManage && <th />}
              </tr>
            </thead>
            <tbody>
              {result?.items.map((product) => (
                <tr key={product.id}>
                  <td>
                    <strong>{product.name}</strong>
                    <small className="table-subtitle">
                      {product.code}{product.barcode ? ` · ${product.barcode}` : ''}
                    </small>
                  </td>
                  <td>{product.categoryName}</td>
                  <td>
                    {currency.format(product.salePrice)}
                    <small className="table-subtitle">Custo {currency.format(product.costPrice)}</small>
                  </td>
                  <td>
                    <span className={product.isActive ? 'status-pill active' : 'status-pill inactive'}>
                      {product.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                  </td>
                  {canManage && (
                    <td className="table-actions">
                      <button className="link-button" type="button" onClick={() => startEditing(product)}>
                        Editar
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {result && (
          <Pagination
            page={result.page}
            totalPages={result.totalPages}
            totalCount={result.totalCount}
            onPageChange={(page) => setQuery((current) => ({ ...current, page }))}
          />
        )}
      </article>

      {canManage && (
        <aside className="panel form-panel">
          <span className="eyebrow">{editing ? 'Alteração com rowversion' : 'Novo cadastro'}</span>
          <h2>{editing ? 'Editar produto' : 'Adicionar produto'}</h2>
          <form
            className="form-stack"
            onSubmit={handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              const payload = {
                ...values,
                barcode: values.barcode.trim() || undefined,
                description: values.description.trim() || undefined,
              }
              try {
                if (editing) {
                  await updateProduct(editing.id, { ...payload, rowVersion: editing.rowVersion })
                  setMessage('Produto atualizado.')
                } else {
                  await createProduct(payload)
                  setMessage('Produto criado.')
                }
                clearForm()
                await load(query)
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              }
            })}
          >
            <div className="form-columns">
              <label>
                Código
                <input {...register('code', { required: 'Informe o código.', maxLength: 30 })} />
                {errors.code && <small className="field-error">{errors.code.message}</small>}
              </label>
              <label>
                Código de barras
                <input
                  inputMode="numeric"
                  {...register('barcode', {
                    pattern: {
                      value: /^(?:\d{8}|\d{12}|\d{13}|\d{14})?$/,
                      message: 'Use 8, 12, 13 ou 14 dígitos.',
                    },
                  })}
                />
                {errors.barcode && <small className="field-error">{errors.barcode.message}</small>}
              </label>
            </div>
            <label>
              Nome
              <input {...register('name', { required: 'Informe o nome.', maxLength: 160 })} />
              {errors.name && <small className="field-error">{errors.name.message}</small>}
            </label>
            <label>
              Descrição
              <textarea rows={3} {...register('description', { maxLength: 1000 })} />
            </label>
            <label>
              Categoria
              <select
                {...register('categoryId', {
                  valueAsNumber: true,
                  min: { value: 1, message: 'Selecione a categoria.' },
                })}
              >
                <option value={0}>Selecione</option>
                {categories.map((category) => (
                  <option value={category.id} key={category.id} disabled={!category.isActive && category.id !== editing?.categoryId}>
                    {category.name}{category.isActive ? '' : ' (inativa)'}
                  </option>
                ))}
              </select>
              {errors.categoryId && <small className="field-error">{errors.categoryId.message}</small>}
            </label>
            <div className="form-columns">
              <label>
                Preço de custo
                <input
                  type="number"
                  min="0"
                  step="0.0001"
                  {...register('costPrice', { valueAsNumber: true, min: 0, required: true })}
                />
              </label>
              <label>
                Preço de venda
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  {...register('salePrice', { valueAsNumber: true, min: 0, required: true })}
                />
              </label>
            </div>
            {editing && (
              <label className="checkbox-row">
                <input type="checkbox" {...register('isActive')} /> Produto ativo
              </label>
            )}
            {message && <div className="form-success">{message}</div>}
            <div className="button-row">
              <button className="button button-primary" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Salvando…' : 'Salvar'}
              </button>
              {editing && (
                <button className="button button-secondary" type="button" onClick={clearForm}>Cancelar</button>
              )}
            </div>
          </form>
        </aside>
      )}
    </section>
  )
}
