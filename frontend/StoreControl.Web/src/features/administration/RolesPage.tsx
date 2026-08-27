import { useCallback, useEffect, useState } from 'react'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { permissions as permissionCodes } from '../auth/authTypes'
import { useAuth } from '../auth/useAuth'
import { listPermissions, listRoles, updateRolePermissions } from './adminService'
import type { PermissionSummary, RoleSummary } from './adminTypes'

export function RolesPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(permissionCodes.rolesManage)
  const [roles, setRoles] = useState<RoleSummary[]>([])
  const [permissions, setPermissions] = useState<PermissionSummary[]>([])
  const [selection, setSelection] = useState<Record<string, string[]>>({})
  const [savingRoleId, setSavingRoleId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const load = useCallback(async (signal?: AbortSignal) => {
    const [roleData, permissionData] = await Promise.all([
      listRoles(signal),
      listPermissions(signal),
    ])
    setRoles(roleData)
    setPermissions(permissionData)
    setSelection(Object.fromEntries(roleData.map((role) => [role.id, role.permissions])))
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // oxlint-disable-next-line react/set-state-in-effect
    void load(controller.signal).catch((requestError) => setError(getProblemMessage(requestError)))
    return () => controller.abort()
  }, [load])

  function toggle(roleId: string, permission: string) {
    setSelection((current) => {
      const rolePermissions = current[roleId] ?? []
      return {
        ...current,
        [roleId]: rolePermissions.includes(permission)
          ? rolePermissions.filter((code) => code !== permission)
          : [...rolePermissions, permission],
      }
    })
  }

  return (
    <section>
      <div className="section-heading page-section-heading">
        <div>
          <span className="eyebrow">Autorização</span>
          <h2>Perfis e permissões</h2>
          <p>Os códigos abaixo são emitidos como claims no JWT e exigidos por policies na API.</p>
        </div>
      </div>
      {error && <div className="form-error page-message">{error}</div>}
      {message && <div className="form-success page-message">{message}</div>}
      <div className="role-grid">
        {roles.map((role) => {
          const locked = role.name === 'Administrador'
          return (
            <article className="panel role-card" key={role.id}>
              <div className="section-heading">
                <div>
                  <span className="eyebrow">Perfil do sistema</span>
                  <h2>{role.name}</h2>
                </div>
                {locked && <span className="status-pill active">Protegido</span>}
              </div>
              <div className="permission-list">
                {permissions.map((permission) => (
                  <label className="permission-item" key={permission.code}>
                    <input
                      type="checkbox"
                      checked={(selection[role.id] ?? []).includes(permission.code)}
                      disabled={!canManage || locked}
                      onChange={() => toggle(role.id, permission.code)}
                    />
                    <span>
                      <code>{permission.code}</code>
                      <small>{permission.description}</small>
                    </span>
                  </label>
                ))}
              </div>
              {canManage && !locked && (
                <button
                  className="button button-secondary"
                  type="button"
                  disabled={savingRoleId === role.id}
                  onClick={async () => {
                    setSavingRoleId(role.id)
                    setError(null)
                    setMessage(null)
                    try {
                      await updateRolePermissions(role.id, selection[role.id] ?? [])
                      setMessage(
                        'Permissões de ' +
                          role.name +
                          ' atualizadas; sessões antigas foram invalidadas.',
                      )
                      await load()
                    } catch (requestError) {
                      setError(getProblemMessage(requestError))
                    } finally {
                      setSavingRoleId(null)
                    }
                  }}
                >
                  {savingRoleId === role.id ? 'Salvando…' : 'Salvar permissões'}
                </button>
              )}
            </article>
          )
        })}
      </div>
    </section>
  )
}
