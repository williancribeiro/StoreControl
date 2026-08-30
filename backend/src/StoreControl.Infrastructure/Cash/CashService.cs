using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Application.Cash;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Cash;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Cash;

internal sealed class CashService(
    StoreControlDbContext dbContext,
    ILogger<CashService> logger) : ICashService
{
    private static readonly string[] SessionSorts = ["openedAt", "closedAt", "store", "status", "difference"];

    public async Task<IReadOnlyCollection<CashRegisterSummary>> ListRegistersAsync(
        Guid userId,
        int? storeId,
        CancellationToken cancellationToken)
    {
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureRequestedStoreIsAccessible(storeId, accessibleStoreIds);
        var query = dbContext.CashRegisters
            .AsNoTracking()
            .Include(register => register.Store)
            .Where(register => accessibleStoreIds.Contains(register.StoreId));
        if (storeId.HasValue)
        {
            query = query.Where(register => register.StoreId == storeId.Value);
        }

        return await query
            .OrderBy(register => register.Store.Name)
            .ThenBy(register => register.Code)
            .Select(register => new CashRegisterSummary(
                register.Id,
                register.StoreId,
                register.Store.Code,
                register.Store.Name,
                register.Code,
                register.Name,
                register.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CashSessionDetails?> GetCurrentAsync(
        Guid userId,
        int cashRegisterId,
        CancellationToken cancellationToken)
    {
        var register = await GetAccessibleRegisterAsync(userId, cashRegisterId, false, cancellationToken);
        var session = await DetailsQuery(false)
            .SingleOrDefaultAsync(item =>
                item.CashRegisterId == register.Id && item.Status == CashSessionStatus.Open,
                cancellationToken);
        return session is null ? null : await MapDetailsAsync(session, cancellationToken);
    }

    public async Task<PagedResult<CashSessionSummary>> ListSessionsAsync(
        Guid userId,
        CashSessionListQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            SessionSorts,
            query.SortDirection);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            throw new RequestValidationException("fromUtc", "A data inicial não pode ser posterior à data final.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureRequestedStoreIsAccessible(query.StoreId, accessibleStoreIds);
        var sessions = dbContext.CashSessions
            .AsNoTracking()
            .Where(session => accessibleStoreIds.Contains(session.CashRegister.StoreId));
        if (query.StoreId.HasValue)
        {
            sessions = sessions.Where(session => session.CashRegister.StoreId == query.StoreId.Value);
        }

        if (query.CashRegisterId.HasValue)
        {
            sessions = sessions.Where(session => session.CashRegisterId == query.CashRegisterId.Value);
        }

        if (query.Status.HasValue)
        {
            sessions = sessions.Where(session => session.Status == query.Status.Value);
        }

        if (query.FromUtc.HasValue)
        {
            sessions = sessions.Where(session => session.OpenedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            sessions = sessions.Where(session => session.OpenedAtUtc <= query.ToUtc.Value);
        }

        var totalCount = await sessions.CountAsync(cancellationToken);
        sessions = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("closedat", false) => sessions.OrderBy(session => session.ClosedAtUtc).ThenBy(session => session.Id),
            ("closedat", true) => sessions.OrderByDescending(session => session.ClosedAtUtc).ThenByDescending(session => session.Id),
            ("store", false) => sessions.OrderBy(session => session.CashRegister.Store.Name).ThenByDescending(session => session.Id),
            ("store", true) => sessions.OrderByDescending(session => session.CashRegister.Store.Name).ThenByDescending(session => session.Id),
            ("status", false) => sessions.OrderBy(session => session.Status).ThenByDescending(session => session.Id),
            ("status", true) => sessions.OrderByDescending(session => session.Status).ThenByDescending(session => session.Id),
            ("difference", false) => sessions.OrderBy(session => session.DifferenceAmount).ThenByDescending(session => session.Id),
            ("difference", true) => sessions.OrderByDescending(session => session.DifferenceAmount).ThenByDescending(session => session.Id),
            ("openedat", false) => sessions.OrderBy(session => session.OpenedAtUtc),
            _ => sessions.OrderByDescending(session => session.OpenedAtUtc)
        };

        var items = await sessions
            .Include(session => session.CashRegister)
                .ThenInclude(register => register.Store)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var userIds = items.Select(item => item.OpenedByUserId).Distinct().ToArray();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return new PagedResult<CashSessionSummary>(
            items.Select(item => MapSummary(item, userNames[item.OpenedByUserId])).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<CashSessionDetails> GetSessionAsync(
        Guid userId,
        long cashSessionId,
        CancellationToken cancellationToken)
    {
        var session = await DetailsQuery(false)
            .SingleOrDefaultAsync(item => item.Id == cashSessionId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sessão de caixa não encontrada.");
        await EnsureStoreIsAccessibleAsync(userId, session.CashRegister.StoreId, cancellationToken);
        return await MapDetailsAsync(session, cancellationToken);
    }

    public async Task<CashOperationResult> OpenAsync(
        Guid userId,
        string correlationId,
        OpenCashSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        var register = await GetAccessibleRegisterAsync(userId, command.CashRegisterId, true, cancellationToken);
        var existing = await DetailsQuery(false)
            .SingleOrDefaultAsync(session => session.OperationId == command.OperationId, cancellationToken);
        if (existing is not null)
        {
            EnsureOpenReplayMatches(existing, userId, command);
            return new CashOperationResult(await MapDetailsAsync(existing, cancellationToken), true);
        }

        CashSession session;
        try
        {
            session = new CashSession(
                command.OperationId,
                register.Id,
                userId,
                command.OpeningAmount,
                correlationId);
            dbContext.CashSessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            existing = await DetailsQuery(false)
                .SingleOrDefaultAsync(item => item.OperationId == command.OperationId, cancellationToken);
            if (existing is not null)
            {
                EnsureOpenReplayMatches(existing, userId, command);
                return new CashOperationResult(await MapDetailsAsync(existing, cancellationToken), true);
            }

            throw new ResourceConflictException("Este caixa já possui uma sessão aberta.");
        }

        logger.LogInformation(
            "Cash session {CashSessionId} opened for register {CashRegisterId}",
            session.Id,
            session.CashRegisterId);
        return new CashOperationResult(await GetSessionAsync(userId, session.Id, cancellationToken), false);
    }

    public async Task<CashOperationResult> AddMovementAsync(
        Guid userId,
        string correlationId,
        long cashSessionId,
        AddCashMovementCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var wasReplay = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var session = await LockedDetailsQuery(cashSessionId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ResourceNotFoundException("Sessão de caixa não encontrada.");
                EnsureStoreIsAccessible(session.CashRegister.StoreId, accessibleStoreIds);
                var existingMovement = session.Movements.SingleOrDefault(item => item.OperationId == command.OperationId);
                if (existingMovement is not null)
                {
                    EnsureMovementReplayMatches(existingMovement, session.Id, userId, command);
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }

                ApplyRowVersion(session, command.RowVersion);
                session.RecordManualMovement(
                    command.OperationId,
                    command.Type,
                    command.Amount,
                    command.Reason,
                    userId,
                    correlationId);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "A sessão de caixa foi alterada por outra operação. Recarregue os dados antes de tentar novamente.");
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException("A movimentação não foi concluída por um conflito de persistência.");
            }
            catch (ArgumentException exception)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw Validation(exception);
            }
            catch (InvalidOperationException exception)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(exception.Message);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });

        logger.LogInformation(
            "Cash movement {OperationId} recorded for session {CashSessionId}",
            command.OperationId,
            cashSessionId);
        return new CashOperationResult(
            await GetSessionAsync(userId, cashSessionId, cancellationToken),
            wasReplay);
    }

    public async Task<CashSessionDetails> CloseAsync(
        Guid userId,
        long cashSessionId,
        CloseCashSessionCommand command,
        CancellationToken cancellationToken)
    {
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var session = await LockedDetailsQuery(cashSessionId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ResourceNotFoundException("Sessão de caixa não encontrada.");
                EnsureStoreIsAccessible(session.CashRegister.StoreId, accessibleStoreIds);
                ApplyRowVersion(session, command.RowVersion);
                session.Close(userId, command.DeclaredClosingAmount);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "A sessão de caixa foi alterada por outra operação. Recarregue os dados antes de fechar.");
            }
            catch (ArgumentException exception)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw Validation(exception);
            }
            catch (InvalidOperationException exception)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(exception.Message);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });

        logger.LogInformation("Cash session {CashSessionId} closed", cashSessionId);
        return await GetSessionAsync(userId, cashSessionId, cancellationToken);
    }

    private IQueryable<CashSession> DetailsQuery(bool tracking)
    {
        var query = dbContext.CashSessions
            .Include(session => session.CashRegister)
                .ThenInclude(register => register.Store)
            .Include(session => session.Movements)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<CashSession> LockedDetailsQuery(long cashSessionId) =>
        dbContext.CashSessions
            .FromSqlInterpolated($"""
                SELECT *
                FROM [cash].[CashSessions] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = {cashSessionId}
                """)
            .Include(session => session.CashRegister)
                .ThenInclude(register => register.Store)
            .Include(session => session.Movements)
            .AsSplitQuery();

    private async Task<CashSessionDetails> MapDetailsAsync(
        CashSession session,
        CancellationToken cancellationToken)
    {
        var userIds = session.Movements.Select(item => item.CreatedByUserId)
            .Append(session.OpenedByUserId)
            .Concat(session.ClosedByUserId.HasValue ? [session.ClosedByUserId.Value] : [])
            .Distinct()
            .ToArray();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return new CashSessionDetails(
            session.Id,
            session.OperationId,
            session.CashRegisterId,
            session.CashRegister.Code,
            session.CashRegister.Name,
            session.CashRegister.StoreId,
            session.CashRegister.Store.Code,
            session.CashRegister.Store.Name,
            session.Status.ToString(),
            session.OpenedByUserId,
            userNames[session.OpenedByUserId],
            session.OpenedAtUtc,
            session.ClosedByUserId,
            session.ClosedByUserId.HasValue ? userNames[session.ClosedByUserId.Value] : null,
            session.ClosedAtUtc,
            session.OpeningAmount,
            session.ExpectedCashAmount,
            session.DeclaredClosingAmount,
            session.DifferenceAmount,
            Convert.ToBase64String(session.RowVersion),
            session.Movements
                .OrderByDescending(movement => movement.OccurredAtUtc)
                .ThenByDescending(movement => movement.Id)
                .Select(movement => new CashMovementSummary(
                    movement.Id,
                    movement.Type.ToString(),
                    movement.PaymentMethod?.ToString(),
                    movement.Amount,
                    movement.Reason,
                    movement.CreatedByUserId,
                    userNames[movement.CreatedByUserId],
                    movement.OccurredAtUtc,
                    movement.CorrelationId,
                    movement.ReferenceType,
                    movement.ReferenceId))
                .ToArray());
    }

    private static CashSessionSummary MapSummary(CashSession session, string openedByUserName) =>
        new(
            session.Id,
            session.CashRegisterId,
            session.CashRegister.Code,
            session.CashRegister.StoreId,
            session.CashRegister.Store.Code,
            session.CashRegister.Store.Name,
            session.Status.ToString(),
            session.OpenedAtUtc,
            session.ClosedAtUtc,
            openedByUserName,
            session.OpeningAmount,
            session.ExpectedCashAmount,
            session.DeclaredClosingAmount,
            session.DifferenceAmount,
            Convert.ToBase64String(session.RowVersion));

    private async Task<CashRegister> GetAccessibleRegisterAsync(
        Guid userId,
        int cashRegisterId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var register = await dbContext.CashRegisters
            .AsNoTracking()
            .Include(item => item.Store)
            .SingleOrDefaultAsync(item => item.Id == cashRegisterId, cancellationToken)
            ?? throw new ResourceNotFoundException("Caixa não encontrado.");
        await EnsureStoreIsAccessibleAsync(userId, register.StoreId, cancellationToken);
        if (requireActive && (!register.IsActive || !register.Store.IsActive))
        {
            throw new RequestValidationException("cashRegisterId", "O caixa e sua loja devem estar ativos.");
        }

        return register;
    }

    private async Task<HashSet<int>> GetAccessibleStoreIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await IsAdministratorAsync(userId, cancellationToken))
        {
            return (await dbContext.Stores.AsNoTracking().Select(store => store.Id).ToListAsync(cancellationToken))
                .ToHashSet();
        }

        return (await dbContext.UserStoreRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && assignment.Store.IsActive)
            .Select(assignment => assignment.StoreId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private async Task EnsureStoreIsAccessibleAsync(
        Guid userId,
        int storeId,
        CancellationToken cancellationToken) =>
        EnsureStoreIsAccessible(storeId, await GetAccessibleStoreIdsAsync(userId, cancellationToken));

    private Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == RoleNames.Administrator
         select userRole).AnyAsync(cancellationToken);

    private void ApplyRowVersion(CashSession session, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new RequestValidationException("rowVersion", "Informe a versão atual da sessão de caixa.");
        }

        dbContext.Entry(session).Property(item => item.RowVersion).OriginalValue =
            MasterDataPersistence.ParseRowVersion(rowVersion);
    }

    private static void EnsureRequestedStoreIsAccessible(int? storeId, IReadOnlySet<int> accessibleStoreIds)
    {
        if (storeId.HasValue)
        {
            EnsureStoreIsAccessible(storeId.Value, accessibleStoreIds);
        }
    }

    private static void EnsureStoreIsAccessible(int storeId, IReadOnlySet<int> accessibleStoreIds)
    {
        if (!accessibleStoreIds.Contains(storeId))
        {
            throw new OperationForbiddenException("O usuário não possui acesso à loja informada.");
        }
    }

    private static void EnsureOpenReplayMatches(
        CashSession session,
        Guid userId,
        OpenCashSessionCommand command)
    {
        if (session.CashRegisterId != command.CashRegisterId ||
            session.OpenedByUserId != userId ||
            session.OpeningAmount != decimal.Round(command.OpeningAmount, 2, MidpointRounding.AwayFromZero))
        {
            throw new ResourceConflictException("A chave de idempotência já foi usada com outros dados de abertura.");
        }
    }

    private static void EnsureMovementReplayMatches(
        CashMovement movement,
        long cashSessionId,
        Guid userId,
        AddCashMovementCommand command)
    {
        var signedAmount = command.Type == CashMovementType.CashOut
            ? -decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero);
        if (movement.CashSessionId != cashSessionId ||
            movement.CreatedByUserId != userId ||
            movement.Type != command.Type ||
            movement.Amount != signedAmount ||
            !string.Equals(movement.Reason, command.Reason.Trim(), StringComparison.Ordinal))
        {
            throw new ResourceConflictException("A chave de idempotência já foi usada com outra movimentação.");
        }
    }

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "cash", exception.Message);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
