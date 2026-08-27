import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { createStore, listStores, updateStore } from './adminService'
import type { StoreSummary } from './adminTypes'

type StoreForm = {
  code: string
  name: string
  isActive: boolean
}

export function StoresPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(permissions.storesManage)
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [editing, setEditing] = useState<StoreSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<StoreForm>({ defaultValues: { code: '', name: '', isActive: true } })

  const load = useCallback(async (signal?: AbortSignal) => {
    setStores(await listStores(signal))
    setLoading(false)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load(controller.signal).catch((requestError) => {
      setError(getProblemMessage(requestError))
      setLoading(false)
    })
    return () => controller.abort()
  }, [load])

  function startEditing(store: StoreSummary) {
    setEditing(store)
    reset({ code: store.code, name: store.name, isActive: store.isActive })
    setMessage(null)
    setError(null)
  }

  function clearForm() {
    setEditing(null)
    reset({ code: '', name: '', isActive: true })
  }

  return (
    <section className="management-layout">
      <article className="panel table-panel">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Administração</span>
            <h2>Lojas e filiais</h2>
          </div>
          <span className="count-badge">{stores.length} registros</span>
        </div>
        {loading ? (
          <p>Carregando lojas…</p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Loja</th>
                  <th>Status</th>
                  {canManage && <th />}
                </tr>
              </thead>
              <tbody>
                {stores.map((store) => (
                  <tr key={store.id}>
                    <td>
                      <code>{store.code}</code>
                    </td>
                    <td>{store.name}</td>
                    <td>
                      <span className={store.isActive ? 'status-pill active' : 'status-pill inactive'}>
                        {store.isActive ? 'Ativa' : 'Inativa'}
                      </span>
                    </td>
                    {canManage && (
                      <td className="table-actions">
                        <button className="link-button" type="button" onClick={() => startEditing(store)}>
                          Editar
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </article>

      {canManage && (
        <aside className="panel form-panel">
          <span className="eyebrow">{editing ? 'Alteração com rowversion' : 'Novo cadastro'}</span>
          <h2>{editing ? 'Editar loja' : 'Adicionar loja'}</h2>
          <form
            className="form-stack"
            onSubmit={handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              try {
                if (editing) {
                  await updateStore(editing.id, { ...values, rowVersion: editing.rowVersion })
                  setMessage('Loja atualizada.')
                } else {
                  await createStore(values)
                  setMessage('Loja criada.')
                }
                clearForm()
                await load()
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              }
            })}
          >
            <label>
              Código
              <input
                {...register('code', {
                  required: 'Informe o código.',
                  maxLength: { value: 20, message: 'Use no máximo 20 caracteres.' },
                })}
              />
              {errors.code && <small className="field-error">{errors.code.message}</small>}
            </label>
            <label>
              Nome
              <input
                {...register('name', {
                  required: 'Informe o nome.',
                  maxLength: { value: 120, message: 'Use no máximo 120 caracteres.' },
                })}
              />
              {errors.name && <small className="field-error">{errors.name.message}</small>}
            </label>
            {editing && (
              <label className="checkbox-row">
                <input type="checkbox" {...register('isActive')} />
                Loja ativa
              </label>
            )}
            {error && <div className="form-error">{error}</div>}
            {message && <div className="form-success">{message}</div>}
            <div className="button-row">
              <button className="button button-primary" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Salvando…' : 'Salvar'}
              </button>
              {editing && (
                <button className="button button-secondary" type="button" onClick={clearForm}>
                  Cancelar
                </button>
              )}
            </div>
          </form>
        </aside>
      )}
    </section>
  )
}
