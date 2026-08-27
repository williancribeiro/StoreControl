import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { Pagination } from './Pagination'
import { createCategory, listCategories, updateCategory } from './registrationService'
import type { CategorySummary, ListQuery, PagedResult } from './registrationTypes'

type CategoryForm = {
  code: string
  name: string
  description: string
  isActive: boolean
}

const initialQuery: ListQuery = {
  page: 1,
  pageSize: 10,
  sortBy: 'name',
  sortDirection: 'asc',
}

export function CategoriesPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(permissions.categoriesManage)
  const [result, setResult] = useState<PagedResult<CategorySummary> | null>(null)
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [editing, setEditing] = useState<CategorySummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CategoryForm>({
    defaultValues: { code: '', name: '', description: '', isActive: true },
  })

  const load = useCallback(async (activeQuery: ListQuery, signal?: AbortSignal) => {
    setResult(await listCategories(activeQuery, signal))
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load(query, controller.signal).catch((requestError) => {
      if (!controller.signal.aborted) setError(getProblemMessage(requestError))
    })
    return () => controller.abort()
  }, [load, query])

  function clearForm() {
    setEditing(null)
    reset({ code: '', name: '', description: '', isActive: true })
  }

  function startEditing(category: CategorySummary) {
    setEditing(category)
    reset({
      code: category.code,
      name: category.name,
      description: category.description ?? '',
      isActive: category.isActive,
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
    }))
  }

  return (
    <section className="management-layout">
      <article className="panel table-panel">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Cadastros</span>
            <h2>Categorias</h2>
          </div>
          <span className="count-badge">{result?.totalCount ?? 0} registros</span>
        </div>
        <div className="filter-grid filter-grid-compact">
          <label>
            Busca
            <input
              value={search}
              placeholder="Código, nome ou descrição"
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && applyFilters()}
            />
          </label>
          <label>
            Status
            <select value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="all">Todos</option>
              <option value="active">Ativas</option>
              <option value="inactive">Inativas</option>
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
              <option value="createdAt:desc">Mais recentes</option>
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
                <th>Código</th>
                <th>Categoria</th>
                <th>Status</th>
                {canManage && <th />}
              </tr>
            </thead>
            <tbody>
              {result?.items.map((category) => (
                <tr key={category.id}>
                  <td><code>{category.code}</code></td>
                  <td>
                    {category.name}
                    {category.description && <small className="table-subtitle">{category.description}</small>}
                  </td>
                  <td>
                    <span className={category.isActive ? 'status-pill active' : 'status-pill inactive'}>
                      {category.isActive ? 'Ativa' : 'Inativa'}
                    </span>
                  </td>
                  {canManage && (
                    <td className="table-actions">
                      <button className="link-button" type="button" onClick={() => startEditing(category)}>
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
          <h2>{editing ? 'Editar categoria' : 'Adicionar categoria'}</h2>
          <form
            className="form-stack"
            onSubmit={handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              try {
                if (editing) {
                  await updateCategory(editing.id, { ...values, rowVersion: editing.rowVersion })
                  setMessage('Categoria atualizada.')
                } else {
                  await createCategory(values)
                  setMessage('Categoria criada.')
                }
                clearForm()
                await load(query)
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              }
            })}
          >
            <label>
              Código
              <input {...register('code', { required: 'Informe o código.', maxLength: 30 })} />
              {errors.code && <small className="field-error">{errors.code.message}</small>}
            </label>
            <label>
              Nome
              <input {...register('name', { required: 'Informe o nome.', maxLength: 120 })} />
              {errors.name && <small className="field-error">{errors.name.message}</small>}
            </label>
            <label>
              Descrição
              <textarea rows={3} {...register('description', { maxLength: 500 })} />
            </label>
            {editing && (
              <label className="checkbox-row">
                <input type="checkbox" {...register('isActive')} /> Categoria ativa
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
