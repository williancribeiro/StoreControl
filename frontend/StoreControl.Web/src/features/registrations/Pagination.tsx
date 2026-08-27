type PaginationProps = {
  page: number
  totalPages: number
  totalCount: number
  onPageChange: (page: number) => void
}

export function Pagination({ page, totalPages, totalCount, onPageChange }: PaginationProps) {
  return (
    <div className="pagination" aria-label="Paginação">
      <span>
        Página {page} de {Math.max(totalPages, 1)} · {totalCount} registros
      </span>
      <div className="button-row">
        <button
          className="button button-secondary"
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Anterior
        </button>
        <button
          className="button button-secondary"
          type="button"
          disabled={totalPages === 0 || page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Próxima
        </button>
      </div>
    </div>
  )
}
