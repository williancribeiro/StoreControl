import { useCallback, useEffect, useState } from 'react'
import axios from 'axios'
import { getSmokeStatus, type SmokeResponse } from './smokeService'

type RequestState =
  | { status: 'loading' }
  | { status: 'success'; data: SmokeResponse }
  | { status: 'error'; message: string }

const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'medium',
  timeZone: 'America/Sao_Paulo',
})

export function SmokePage() {
  const [requestState, setRequestState] = useState<RequestState>({ status: 'loading' })
  const [reloadKey, setReloadKey] = useState(0)

  const loadStatus = useCallback(async (signal: AbortSignal) => {
    try {
      const data = await getSmokeStatus(signal)
      setRequestState({ status: 'success', data })
    } catch (error) {
      if (axios.isCancel(error)) {
        return
      }

      const message = axios.isAxiosError(error)
        ? (error.response?.data as { detail?: string } | undefined)?.detail ?? error.message
        : 'Falha inesperada ao consultar a API.'

      setRequestState({ status: 'error', message })
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // The request lifecycle is the external system this effect synchronizes with.
    // oxlint-disable-next-line react/set-state-in-effect
    void loadStatus(controller.signal)

    return () => controller.abort()
  }, [loadStatus, reloadKey])

  return (
    <section className="foundation-grid" aria-live="polite">
      <article className="panel status-panel">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Smoke test ponta a ponta</span>
            <h2>React → API → SQL Server</h2>
          </div>
          <StatusIndicator state={requestState.status} />
        </div>

        {requestState.status === 'loading' && (
          <div className="status-message">Consultando a API e o banco de dados…</div>
        )}

        {requestState.status === 'error' && (
          <div className="error-box">
            <strong>Não foi possível completar o smoke test.</strong>
            <p>{requestState.message}</p>
            <button
              type="button"
              onClick={() => {
                setRequestState({ status: 'loading' })
                setReloadKey((current) => current + 1)
              }}
            >
              Tentar novamente
            </button>
          </div>
        )}

        {requestState.status === 'success' && (
          <dl className="status-details">
            <div>
              <dt>Aplicação</dt>
              <dd>{requestState.data.application}</dd>
            </div>
            <div>
              <dt>API</dt>
              <dd className="value-ok">{requestState.data.status}</dd>
            </div>
            <div>
              <dt>SQL Server</dt>
              <dd className="value-ok">{requestState.data.database.status}</dd>
            </div>
            <div>
              <dt>Foundation schema</dt>
              <dd>v{requestState.data.database.foundationVersion}</dd>
            </div>
            <div>
              <dt>Hora do banco</dt>
              <dd>{dateFormatter.format(new Date(requestState.data.database.serverUtcTime))}</dd>
            </div>
          </dl>
        )}
      </article>

      <article className="panel architecture-panel">
        <span className="eyebrow">Estrutura ativa</span>
        <h2>Monólito modular</h2>
        <p>
          A fundação mantém o frontend separado e concentra as regras futuras em módulos de negócio
          dentro das camadas Domain, Application e Infrastructure.
        </p>
        <ol className="request-path">
          <li>React + TypeScript</li>
          <li>ASP.NET Core Web API</li>
          <li>Entity Framework Core</li>
          <li>SQL Server</li>
        </ol>
      </article>

      <article className="panel scope-panel">
        <span className="eyebrow">Escopo desta entrega</span>
        <h2>Infraestrutura antes do domínio</h2>
        <p>
          Health checks, OpenAPI, tratamento de erros, migration inicial, Worker e ambiente Docker
          estão preparados. Os módulos de vendas, estoque e caixa permanecem deliberadamente
          pendentes para as próximas fases.
        </p>
      </article>
    </section>
  )
}

function StatusIndicator({ state }: { state: RequestState['status'] }) {
  const label = state === 'success' ? 'Operacional' : state === 'error' ? 'Indisponível' : 'Verificando'

  return <span className={`status-indicator status-${state}`}>{label}</span>
}
