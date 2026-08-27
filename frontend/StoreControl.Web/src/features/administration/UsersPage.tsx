import { useCallback, useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { permissions } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import {
  createUser,
  listRoles,
  listStores,
  listUsers,
  resetUserPassword,
  updateUser,
} from './adminService'
import type { RoleSummary, StoreSummary, UserSummary } from './adminTypes'

type UserForm = {
  fullName: string
  email: string
  password: string
  isActive: boolean
  isAdministrator: boolean
}

const emptyUser: UserForm = {
  fullName: '',
  email: '',
  password: '',
  isActive: true,
  isAdministrator: false,
}

export function UsersPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(permissions.usersManage)
  const [users, setUsers] = useState<UserSummary[]>([])
  const [roles, setRoles] = useState<RoleSummary[]>([])
  const [stores, setStores] = useState<StoreSummary[]>([])
  const [editing, setEditing] = useState<UserSummary | null>(null)
  const [storeRoles, setStoreRoles] = useState<Record<number, string>>({})
  const [search, setSearch] = useState('')
  const [passwordReset, setPasswordReset] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<UserForm>({ defaultValues: emptyUser })

  const load = useCallback(async (searchTerm = '', signal?: AbortSignal) => {
    const [userData, roleData, storeData] = await Promise.all([
      listUsers(searchTerm, signal),
      listRoles(signal),
      listStores(signal),
    ])
    setUsers(userData.items)
    setRoles(roleData)
    setStores(storeData)
    setLoading(false)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load('', controller.signal).catch((requestError) => {
      setError(getProblemMessage(requestError))
      setLoading(false)
    })
    return () => controller.abort()
  }, [load])

  const storeRoleOptions = roles.filter((role) => role.name !== 'Administrador')

  function clearForm() {
    setEditing(null)
    setStoreRoles({})
    setPasswordReset('')
    reset(emptyUser)
  }

  function startEditing(user: UserSummary) {
    setEditing(user)
    setStoreRoles(
      Object.fromEntries(
        user.storeAssignments.map((assignment) => [assignment.storeId, assignment.roleId]),
      ),
    )
    reset({
      fullName: user.fullName,
      email: user.email,
      password: '',
      isActive: user.isActive,
      isAdministrator: user.isAdministrator,
    })
    setMessage(null)
    setError(null)
  }

  const assignmentPayload = () =>
    Object.entries(storeRoles)
      .filter(([, roleId]) => roleId)
      .map(([storeId, roleId]) => ({ storeId: Number(storeId), roleId }))

  return (
    <section className="management-layout management-layout-users">
      <article className="panel table-panel">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Identidade e acesso</span>
            <h2>Usuários</h2>
          </div>
          <span className="count-badge">{users.length} exibidos</span>
        </div>
        <form
          className="search-row"
          onSubmit={(event) => {
            event.preventDefault()
            setLoading(true)
            void load(search).catch((requestError) => setError(getProblemMessage(requestError)))
          }}
        >
          <input
            aria-label="Buscar usuários"
            placeholder="Buscar por nome ou e-mail"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <button className="button button-secondary" type="submit">
            Buscar
          </button>
        </form>
        {loading ? (
          <p>Carregando usuários…</p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Usuário</th>
                  <th>Perfis</th>
                  <th>Status</th>
                  {canManage && <th />}
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>
                      <strong>{user.fullName}</strong>
                      <small className="table-subtitle">{user.email}</small>
                    </td>
                    <td>
                      <div className="compact-tags">
                        {user.isAdministrator && <span className="tag">Administrador</span>}
                        {user.storeAssignments.map((assignment) => (
                          <span className="tag" key={assignment.storeId}>
                            {assignment.storeCode}: {assignment.roleName}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td>
                      <span className={user.isActive ? 'status-pill active' : 'status-pill inactive'}>
                        {user.isActive ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    {canManage && (
                      <td className="table-actions">
                        <button className="link-button" type="button" onClick={() => startEditing(user)}>
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
          <span className="eyebrow">{editing ? 'Alterar acesso' : 'Novo usuário'}</span>
          <h2>{editing ? editing.fullName : 'Cadastrar usuário'}</h2>
          <form
            className="form-stack"
            onSubmit={handleSubmit(async (values) => {
              setError(null)
              setMessage(null)
              try {
                if (editing) {
                  await updateUser(editing.id, {
                    fullName: values.fullName,
                    isActive: values.isActive,
                    isAdministrator: values.isAdministrator,
                    storeAssignments: assignmentPayload(),
                  })
                  setMessage('Usuário atualizado; sessões anteriores foram invalidadas.')
                } else {
                  await createUser({
                    fullName: values.fullName,
                    email: values.email,
                    password: values.password,
                    isAdministrator: values.isAdministrator,
                    storeAssignments: assignmentPayload(),
                  })
                  setMessage('Usuário criado.')
                }
                clearForm()
                await load(search)
              } catch (requestError) {
                setError(getProblemMessage(requestError))
              }
            })}
          >
            <label>
              Nome completo
              <input {...register('fullName', { required: 'Informe o nome.' })} />
              {errors.fullName && <small className="field-error">{errors.fullName.message}</small>}
            </label>
            <label>
              E-mail
              <input
                type="email"
                disabled={Boolean(editing)}
                {...register('email', { required: !editing ? 'Informe o e-mail.' : false })}
              />
              {errors.email && <small className="field-error">{errors.email.message}</small>}
            </label>
            {!editing && (
              <label>
                Senha inicial
                <input
                  type="password"
                  autoComplete="new-password"
                  {...register('password', {
                    required: 'Informe a senha inicial.',
                    minLength: { value: 12, message: 'Use ao menos 12 caracteres.' },
                  })}
                />
                {errors.password && <small className="field-error">{errors.password.message}</small>}
              </label>
            )}
            <label className="checkbox-row">
              <input type="checkbox" {...register('isAdministrator')} />
              Administrador global
            </label>
            {editing && (
              <label className="checkbox-row">
                <input type="checkbox" {...register('isActive')} />
                Usuário ativo
              </label>
            )}

            <fieldset>
              <legend>Perfis por loja</legend>
              {stores
                .filter((store) => store.isActive)
                .map((store) => (
                  <label key={store.id}>
                    {store.name}
                    <select
                      value={storeRoles[store.id] ?? ''}
                      onChange={(event) =>
                        setStoreRoles((current) => ({
                          ...current,
                          [store.id]: event.target.value,
                        }))
                      }
                    >
                      <option value="">Sem acesso</option>
                      {storeRoleOptions.map((role) => (
                        <option value={role.id} key={role.id}>
                          {role.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ))}
            </fieldset>

            {error && <div className="form-error">{error}</div>}
            {message && <div className="form-success">{message}</div>}
            <div className="button-row">
              <button className="button button-primary" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Salvando…' : 'Salvar usuário'}
              </button>
              {editing && (
                <button className="button button-secondary" type="button" onClick={clearForm}>
                  Cancelar
                </button>
              )}
            </div>
          </form>

          {editing && (
            <div className="password-reset">
              <h3>Redefinir senha</h3>
              <p>A redefinição revoga todas as sessões de refresh do usuário.</p>
              <input
                type="password"
                placeholder="Nova senha segura"
                value={passwordReset}
                onChange={(event) => setPasswordReset(event.target.value)}
              />
              <button
                className="button button-danger"
                type="button"
                disabled={passwordReset.length < 12}
                onClick={async () => {
                  setError(null)
                  try {
                    await resetUserPassword(editing.id, passwordReset)
                    setPasswordReset('')
                    setMessage('Senha redefinida e sessões revogadas.')
                  } catch (requestError) {
                    setError(getProblemMessage(requestError))
                  }
                }}
              >
                Redefinir senha
              </button>
            </div>
          )}
        </aside>
      )}
    </section>
  )
}
