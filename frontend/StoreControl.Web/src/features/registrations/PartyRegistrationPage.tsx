import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { useAuth } from '../auth/useAuth'
import { Pagination } from './Pagination'
import type { PartyInput, PartyUpdateInput } from './registrationService'
import type { ListQuery, PagedResult, PartySummary } from './registrationTypes'

type PartyRegistrationPageProps = {
  eyebrow: string
  title: string
  singular: string
  managePermission: string
  list: (query: ListQuery, signal?: AbortSignal) => Promise<PagedResult<PartySummary>>
  create: (input: PartyInput) => Promise<PartySummary>
  update: (id: number, input: PartyUpdateInput) => Promise<PartySummary>
}

type PartyForm = {
  code: string
  name: string
  document: string
  email: string
  phone: string
  isActive: boolean
}

const initialQuery: ListQuery = {
  page: 1,
  pageSize: 10,
  sortBy: 'name',
  sortDirection: 'asc',
}

export function PartyRegistrationPage({
  eyebrow,
  title,
  singular,
  managePermission,
  list,
  create,
  update,
}: PartyRegistrationPageProps) {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(managePermission)
  const subject = singular.charAt(0).toUpperCase() + singular.slice(1)
  const [result, setResult] = useState<PagedResult<PartySummary> | null>(null)
  const [query, setQuery] = useState(initialQuery)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [editing, setEditing] = useState<PartySummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<PartyForm>({
    defaultValues: { code: '', name: '', document: '', email: '', phone: '', isActive: true },
  })

  const load = useCallback(
    async (activeQuery: ListQuery, signal?: AbortSignal) => {
      setResult(await list(activeQuery, signal))
    },
    [list],
  )

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
    reset({ code: '', name: '', document: '', email: '', phone: '', isActive: true })
  }

  function startEditing(party: PartySummary) {
    setEditing(party)
    reset({
      code: party.code,
      name: party.name,
      document: party.document ?? '',
      email: party.email ?? '',
      phone: party.phone ?? '',
      isActive: party.isActive,
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
            <span className="eyebrow">{eyebrow}</span>
            <h2>{title}</h2>
          </div>
          <span className="count-badge">{result?.totalCount ?? 0} registros</span>
        </div>
        <div className="filter-grid filter-grid-compact">
          <label>
            Busca
            <input
              value={search}
              placeholder="Código, nome, documento ou contato"
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && applyFilters()}
            />
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
                <th>Nome</th>
                <th>Documento</th>
                <th>Contato</th>
                <th>Status</th>
                {canManage && <th />}
              </tr>
            </thead>
            <tbody>
              {result?.items.map((party) => (
                <tr key={party.id}>
                  <td><code>{party.code}</code></td>
                  <td>{party.name}</td>
                  <td>{party.document ?? '—'}</td>
                  <td>
                    {party.email ?? '—'}
                    {party.phone && <small className="table-subtitle">{party.phone}</small>}
                  </td>
                  <td>
                    <span className={party.isActive ? 'status-pill active' : 'status-pill inactive'}>
                      {party.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                  </td>
                  {canManage && (
                    <td className="table-actions">
                      <button className="link-button" type="button" onClick={() => startEditing(party)}>
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
          <h2>{editing ? `Editar ${singular}` : `Adicionar ${singular}`}</h2>
          <form
            className="form-stack"
            onSubmit={handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              const payload = {
                code: values.code,
                name: values.name,
                document: values.document.trim() || undefined,
                email: values.email.trim() || undefined,
                phone: values.phone.trim() || undefined,
              }
              try {
                if (editing) {
                  await update(editing.id, {
                    ...payload,
                    isActive: values.isActive,
                    rowVersion: editing.rowVersion,
                  })
                  setMessage(`${subject} atualizado.`)
                } else {
                  await create(payload)
                  setMessage(`${subject} criado.`)
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
              <input {...register('name', { required: 'Informe o nome.', maxLength: 160 })} />
              {errors.name && <small className="field-error">{errors.name.message}</small>}
            </label>
            <label>
              CPF/CNPJ
              <input
                {...register('document', {
                  validate: (value) => {
                    const digits = value.replace(/\D/g, '')
                    return !digits || digits.length === 11 || digits.length === 14 || 'Use 11 ou 14 dígitos.'
                  },
                })}
              />
              {errors.document && <small className="field-error">{errors.document.message}</small>}
            </label>
            <label>
              E-mail
              <input type="email" {...register('email', { maxLength: 254 })} />
            </label>
            <label>
              Telefone
              <input {...register('phone', { maxLength: 30 })} />
            </label>
            {editing && (
              <label className="checkbox-row">
                <input type="checkbox" {...register('isActive')} /> Cadastro ativo
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
