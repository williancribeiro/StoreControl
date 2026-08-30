using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Inventory;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Inventory;

internal sealed class StockTransferService(
    StoreControlDbContext dbContext,
    ILogger<StockTransferService> logger) : IStockTransferService
{
    private static readonly string[] AllowedSorts = ["createdAt", "status", "origin", "destination"];

    public async Task<PagedResult<StockTransferSummary>> ListAsync(
        Guid userId,
        StockTransferListQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            AllowedSorts,
            query.SortDirection);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            throw new RequestValidationException("fromUtc", "A data inicial não pode ser posterior à data final.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureRequestedStoreIsAccessible(query.OriginStoreId, accessibleStoreIds);
        EnsureRequestedStoreIsAccessible(query.DestinationStoreId, accessibleStoreIds);
        var transfers = dbContext.StockTransfers
            .AsNoTracking()
            .Where(transfer =>
                accessibleStoreIds.Contains(transfer.OriginStoreId) &&
                accessibleStoreIds.Contains(transfer.DestinationStoreId));
        if (query.OriginStoreId.HasValue)
        {
            transfers = transfers.Where(transfer => transfer.OriginStoreId == query.OriginStoreId.Value);
        }

        if (query.DestinationStoreId.HasValue)
        {
            transfers = transfers.Where(transfer => transfer.DestinationStoreId == query.DestinationStoreId.Value);
        }

        if (query.Status.HasValue)
        {
            transfers = transfers.Where(transfer => transfer.Status == query.Status.Value);
        }

        if (query.FromUtc.HasValue)
        {
            transfers = transfers.Where(transfer => transfer.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            transfers = transfers.Where(transfer => transfer.CreatedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var numericSearch = long.TryParse(search.TrimStart('#'), out var parsedId) ? parsedId : (long?)null;
            transfers = transfers.Where(transfer =>
                (numericSearch.HasValue && transfer.Id == numericSearch.Value) ||
                transfer.OriginStore.Code.Contains(search) ||
                transfer.OriginStore.Name.Contains(search) ||
                transfer.DestinationStore.Code.Contains(search) ||
                transfer.DestinationStore.Name.Contains(search) ||
                transfer.Items.Any(item =>
                    item.ProductCode.Contains(search) || item.ProductName.Contains(search)));
        }

        var totalCount = await transfers.CountAsync(cancellationToken);
        transfers = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("status", false) => transfers.OrderBy(transfer => transfer.Status).ThenByDescending(transfer => transfer.Id),
            ("status", true) => transfers.OrderByDescending(transfer => transfer.Status).ThenByDescending(transfer => transfer.Id),
            ("origin", false) => transfers.OrderBy(transfer => transfer.OriginStore.Name).ThenByDescending(transfer => transfer.Id),
            ("origin", true) => transfers.OrderByDescending(transfer => transfer.OriginStore.Name).ThenByDescending(transfer => transfer.Id),
            ("destination", false) => transfers.OrderBy(transfer => transfer.DestinationStore.Name).ThenByDescending(transfer => transfer.Id),
            ("destination", true) => transfers.OrderByDescending(transfer => transfer.DestinationStore.Name).ThenByDescending(transfer => transfer.Id),
            ("createdat", false) => transfers.OrderBy(transfer => transfer.CreatedAtUtc),
            _ => transfers.OrderByDescending(transfer => transfer.CreatedAtUtc)
        };

        var items = await transfers
            .Include(transfer => transfer.OriginStore)
            .Include(transfer => transfer.DestinationStore)
            .Include(transfer => transfer.Items)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<StockTransferSummary>(
            items.Select(MapSummary).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<StockTransferDetails> GetAsync(
        Guid userId,
        long stockTransferId,
        CancellationToken cancellationToken)
    {
        var transfer = await DetailsQuery(false)
            .SingleOrDefaultAsync(item => item.Id == stockTransferId, cancellationToken)
            ?? throw new ResourceNotFoundException("Transferência não encontrada.");
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureTransferIsAccessible(transfer, accessibleStoreIds);
        return await MapDetailsAsync(transfer, cancellationToken);
    }

    public async Task<StockTransferOperationResult> CreateAsync(
        Guid userId,
        CreateStockTransferCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCreateCommand(command);
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureStoreIsAccessible(command.OriginStoreId, accessibleStoreIds);
        EnsureStoreIsAccessible(command.DestinationStoreId, accessibleStoreIds);
        var replay = await DetailsQuery(false)
            .SingleOrDefaultAsync(transfer => transfer.OperationId == command.OperationId, cancellationToken);
        if (replay is not null)
        {
            EnsureCreateReplayMatches(replay, userId, command);
            return new StockTransferOperationResult(
                await MapDetailsAsync(replay, cancellationToken),
                true);
        }

        var stores = await dbContext.Stores
            .AsNoTracking()
            .Where(store => store.Id == command.OriginStoreId || store.Id == command.DestinationStoreId)
            .ToDictionaryAsync(store => store.Id, cancellationToken);
        if (stores.Count != 2)
        {
            throw new RequestValidationException("stores", "Uma ou mais lojas informadas não existem.");
        }

        if (stores.Values.Any(store => !store.IsActive))
        {
            throw new RequestValidationException("stores", "As lojas de origem e destino devem estar ativas.");
        }

        var productIds = command.Items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length)
        {
            throw new RequestValidationException("items", "Um ou mais produtos informados não existem.");
        }

        if (products.Values.Any(product => !product.IsActive))
        {
            throw new RequestValidationException("items", "Produtos inativos não podem ser transferidos.");
        }

        try
        {
            var transfer = new StockTransfer(
                command.OperationId,
                command.OriginStoreId,
                command.DestinationStoreId,
                userId,
                command.Notes);
            foreach (var input in command.Items)
            {
                var product = products[input.ProductId];
                transfer.AddItem(product.Id, product.Code, product.Name, input.Quantity);
            }

            dbContext.StockTransfers.Add(transfer);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Stock transfer {StockTransferId} created from store {OriginStoreId} to {DestinationStoreId}",
                transfer.Id,
                transfer.OriginStoreId,
                transfer.DestinationStoreId);
            return new StockTransferOperationResult(
                await GetAsync(userId, transfer.Id, cancellationToken),
                false);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            replay = await DetailsQuery(false)
                .SingleOrDefaultAsync(transfer => transfer.OperationId == command.OperationId, cancellationToken);
            if (replay is not null)
            {
                EnsureCreateReplayMatches(replay, userId, command);
                return new StockTransferOperationResult(await MapDetailsAsync(replay, cancellationToken), true);
            }

            throw new ResourceConflictException("A transferência não foi criada por um conflito de persistência.");
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourceConflictException(exception.Message);
        }
    }

    public Task<StockTransferOperationResult> DispatchAsync(
        Guid userId,
        string correlationId,
        long stockTransferId,
        StockTransferOperationCommand command,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            userId,
            correlationId,
            stockTransferId,
            command.OperationId,
            command.RowVersion,
            TransferAction.Dispatch,
            null,
            cancellationToken);

    public Task<StockTransferOperationResult> ReceiveAsync(
        Guid userId,
        string correlationId,
        long stockTransferId,
        StockTransferOperationCommand command,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            userId,
            correlationId,
            stockTransferId,
            command.OperationId,
            command.RowVersion,
            TransferAction.Receive,
            null,
            cancellationToken);

    public Task<StockTransferOperationResult> CancelAsync(
        Guid userId,
        long stockTransferId,
        CancelStockTransferCommand command,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            userId,
            "stock-transfer-cancellation",
            stockTransferId,
            command.OperationId,
            command.RowVersion,
            TransferAction.Cancel,
            command.Reason,
            cancellationToken);

    private async Task<StockTransferOperationResult> ChangeStatusAsync(
        Guid userId,
        string correlationId,
        long stockTransferId,
        Guid operationId,
        string rowVersion,
        TransferAction action,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        var expectedRowVersion = ParseRowVersion(rowVersion);
        var normalizedReason = action == TransferAction.Cancel
            ? NormalizeRequired(reason, "reason", 500)
            : null;
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var operationReplay = await FindOperationReplayAsync(action, operationId, cancellationToken);
                if (operationReplay is not null)
                {
                    EnsureOperationReplayMatches(operationReplay, stockTransferId, userId, action, normalizedReason);
                    EnsureTransferIsAccessible(operationReplay, accessibleStoreIds);
                    await transaction.CommitAsync(cancellationToken);
                    return (operationReplay.Id, true);
                }

                var transfer = await LockedTransferQuery(stockTransferId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ResourceNotFoundException("Transferência não encontrada.");
                EnsureTransferIsAccessible(transfer, accessibleStoreIds);
                if (!transfer.RowVersion.SequenceEqual(expectedRowVersion))
                {
                    throw new ResourceConflictException(
                        "A transferência foi alterada por outra operação. Recarregue os dados.");
                }

                switch (action)
                {
                    case TransferAction.Dispatch:
                        await ApplyDispatchAsync(transfer, userId, correlationId, operationId, cancellationToken);
                        break;
                    case TransferAction.Receive:
                        await ApplyReceiptAsync(transfer, userId, correlationId, operationId, cancellationToken);
                        break;
                    case TransferAction.Cancel:
                        transfer.Cancel(operationId, userId, normalizedReason!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action));
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation(
                    "Stock transfer {StockTransferId} changed by {TransferAction} to status {Status}",
                    transfer.Id,
                    action,
                    transfer.Status);
                return (transfer.Id, false);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "A transferência ou o estoque foi alterado por outra operação. Recarregue os dados.");
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                var replay = await FindOperationReplayAsync(action, operationId, cancellationToken);
                if (replay is not null)
                {
                    EnsureOperationReplayMatches(replay, stockTransferId, userId, action, normalizedReason);
                    return (replay.Id, true);
                }

                throw new ResourceConflictException(
                    "A operação da transferência encontrou um conflito. Nenhuma alteração foi aplicada.");
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

        return new StockTransferOperationResult(
            await GetAsync(userId, result.Id, cancellationToken),
            result.Item2);
    }

    private async Task ApplyDispatchAsync(
        StockTransfer transfer,
        Guid userId,
        string correlationId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var productIds = transfer.Items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
        var balances = await dbContext.StockBalances
            .Where(balance =>
                balance.StoreId == transfer.OriginStoreId && productIds.Contains(balance.ProductId))
            .OrderBy(balance => balance.ProductId)
            .ToDictionaryAsync(balance => balance.ProductId, cancellationToken);
        foreach (var item in transfer.Items.OrderBy(item => item.ProductId))
        {
            if (!balances.TryGetValue(item.ProductId, out var balance))
            {
                throw new ResourceConflictException(
                    $"Estoque insuficiente para o produto {item.ProductCode}. Disponível: 0; solicitado: {item.Quantity:0.###}.");
            }

            dbContext.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.TransferOut,
                item.Quantity,
                $"Saída da transferência {transfer.Id}.",
                userId,
                correlationId,
                "StockTransferDispatch",
                operationId.ToString("D")));
        }

        transfer.Dispatch(operationId, userId);
    }

    private async Task ApplyReceiptAsync(
        StockTransfer transfer,
        Guid userId,
        string correlationId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var productIds = transfer.Items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
        var balances = await dbContext.StockBalances
            .Where(balance =>
                balance.StoreId == transfer.DestinationStoreId && productIds.Contains(balance.ProductId))
            .OrderBy(balance => balance.ProductId)
            .ToDictionaryAsync(balance => balance.ProductId, cancellationToken);
        foreach (var item in transfer.Items.OrderBy(item => item.ProductId))
        {
            if (!balances.TryGetValue(item.ProductId, out var balance))
            {
                balance = new StockBalance(transfer.DestinationStoreId, item.ProductId);
                balances.Add(item.ProductId, balance);
                dbContext.StockBalances.Add(balance);
            }

            dbContext.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.TransferIn,
                item.Quantity,
                $"Entrada da transferência {transfer.Id}.",
                userId,
                correlationId,
                "StockTransferReceipt",
                operationId.ToString("D")));
        }

        transfer.Receive(operationId, userId);
    }

    private IQueryable<StockTransfer> DetailsQuery(bool tracking)
    {
        var query = dbContext.StockTransfers
            .Include(transfer => transfer.OriginStore)
            .Include(transfer => transfer.DestinationStore)
            .Include(transfer => transfer.Items)
            .Include(transfer => transfer.StatusHistory)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<StockTransfer> LockedTransferQuery(long stockTransferId) =>
        dbContext.StockTransfers
            .FromSqlInterpolated($"""
                SELECT *
                FROM [inventory].[StockTransfers] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = {stockTransferId}
                """)
            .Include(transfer => transfer.OriginStore)
            .Include(transfer => transfer.DestinationStore)
            .Include(transfer => transfer.Items)
            .Include(transfer => transfer.StatusHistory)
            .AsSplitQuery();

    private Task<StockTransfer?> FindOperationReplayAsync(
        TransferAction action,
        Guid operationId,
        CancellationToken cancellationToken) => action switch
        {
            TransferAction.Dispatch => DetailsQuery(false).SingleOrDefaultAsync(
                transfer => transfer.DispatchOperationId == operationId,
                cancellationToken),
            TransferAction.Receive => DetailsQuery(false).SingleOrDefaultAsync(
                transfer => transfer.ReceiptOperationId == operationId,
                cancellationToken),
            TransferAction.Cancel => DetailsQuery(false).SingleOrDefaultAsync(
                transfer => transfer.CancellationOperationId == operationId,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    private async Task<StockTransferDetails> MapDetailsAsync(
        StockTransfer transfer,
        CancellationToken cancellationToken)
    {
        var userIds = transfer.StatusHistory
            .Select(history => history.ChangedByUserId)
            .Append(transfer.CreatedByUserId)
            .Concat(transfer.DispatchedByUserId.HasValue ? [transfer.DispatchedByUserId.Value] : [])
            .Concat(transfer.ReceivedByUserId.HasValue ? [transfer.ReceivedByUserId.Value] : [])
            .Concat(transfer.CancelledByUserId.HasValue ? [transfer.CancelledByUserId.Value] : [])
            .Distinct()
            .ToArray();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        return new StockTransferDetails(
            transfer.Id,
            FormatNumber(transfer.Id),
            transfer.OperationId,
            transfer.OriginStoreId,
            transfer.OriginStore.Code,
            transfer.OriginStore.Name,
            transfer.DestinationStoreId,
            transfer.DestinationStore.Code,
            transfer.DestinationStore.Name,
            transfer.CreatedByUserId,
            userNames[transfer.CreatedByUserId],
            transfer.CreatedAtUtc,
            transfer.Status.ToString(),
            transfer.Notes,
            transfer.DispatchOperationId,
            transfer.DispatchedByUserId,
            transfer.DispatchedByUserId.HasValue ? userNames[transfer.DispatchedByUserId.Value] : null,
            transfer.DispatchedAtUtc,
            transfer.ReceiptOperationId,
            transfer.ReceivedByUserId,
            transfer.ReceivedByUserId.HasValue ? userNames[transfer.ReceivedByUserId.Value] : null,
            transfer.ReceivedAtUtc,
            transfer.CancellationOperationId,
            transfer.CancelledByUserId,
            transfer.CancelledByUserId.HasValue ? userNames[transfer.CancelledByUserId.Value] : null,
            transfer.CancelledAtUtc,
            transfer.CancellationReason,
            Convert.ToBase64String(transfer.RowVersion),
            transfer.Items
                .OrderBy(item => item.Id)
                .Select(item => new StockTransferItemSummary(
                    item.Id,
                    item.ProductId,
                    item.ProductCode,
                    item.ProductName,
                    item.Quantity))
                .ToArray(),
            transfer.StatusHistory
                .OrderBy(history => history.ChangedAtUtc)
                .ThenBy(history => history.Id)
                .Select(history => new StockTransferHistorySummary(
                    history.Id,
                    history.Status.ToString(),
                    history.ChangedByUserId,
                    userNames[history.ChangedByUserId],
                    history.ChangedAtUtc,
                    history.Description))
                .ToArray());
    }

    private static StockTransferSummary MapSummary(StockTransfer transfer) => new(
        transfer.Id,
        FormatNumber(transfer.Id),
        transfer.OriginStoreId,
        transfer.OriginStore.Code,
        transfer.OriginStore.Name,
        transfer.DestinationStoreId,
        transfer.DestinationStore.Code,
        transfer.DestinationStore.Name,
        transfer.Status.ToString(),
        transfer.Items.Count,
        transfer.Items.Sum(item => item.Quantity),
        transfer.CreatedAtUtc,
        transfer.DispatchedAtUtc,
        transfer.ReceivedAtUtc,
        Convert.ToBase64String(transfer.RowVersion));

    private static void ValidateCreateCommand(CreateStockTransferCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        if (command.OriginStoreId == command.DestinationStoreId)
        {
            throw new RequestValidationException("destinationStoreId", "As lojas de origem e destino devem ser diferentes.");
        }

        if (command.Items.Count is < 1 or > 100)
        {
            throw new RequestValidationException("items", "Informe entre 1 e 100 itens na transferência.");
        }

        if (command.Items.Select(item => item.ProductId).Distinct().Count() != command.Items.Count)
        {
            throw new RequestValidationException("items", "Um produto não pode aparecer mais de uma vez na transferência.");
        }
    }

    private static void EnsureCreateReplayMatches(
        StockTransfer transfer,
        Guid userId,
        CreateStockTransferCommand command)
    {
        var itemsMatch = transfer.Items.Count == command.Items.Count && command.Items.All(input =>
            transfer.Items.Any(item =>
                item.ProductId == input.ProductId &&
                item.Quantity == decimal.Round(input.Quantity, 3, MidpointRounding.AwayFromZero)));
        if (transfer.CreatedByUserId != userId ||
            transfer.OriginStoreId != command.OriginStoreId ||
            transfer.DestinationStoreId != command.DestinationStoreId ||
            !string.Equals(transfer.Notes, NormalizeOptional(command.Notes), StringComparison.Ordinal) ||
            !itemsMatch)
        {
            throw new ResourceConflictException("A chave de idempotência já foi usada em outra transferência.");
        }
    }

    private static void EnsureOperationReplayMatches(
        StockTransfer transfer,
        long stockTransferId,
        Guid userId,
        TransferAction action,
        string? normalizedReason)
    {
        var actorMatches = action switch
        {
            TransferAction.Dispatch => transfer.DispatchedByUserId == userId,
            TransferAction.Receive => transfer.ReceivedByUserId == userId,
            TransferAction.Cancel => transfer.CancelledByUserId == userId &&
                                     string.Equals(
                                         transfer.CancellationReason,
                                         normalizedReason,
                                         StringComparison.Ordinal),
            _ => false
        };
        if (transfer.Id != stockTransferId || !actorMatches)
        {
            throw new ResourceConflictException("A chave de idempotência já foi usada em outra operação.");
        }
    }

    private async Task<HashSet<int>> GetAccessibleStoreIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
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

    private Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == RoleNames.Administrator
         select userRole).AnyAsync(cancellationToken);

    private static void EnsureRequestedStoreIsAccessible(
        int? storeId,
        IReadOnlySet<int> accessibleStoreIds)
    {
        if (storeId.HasValue)
        {
            EnsureStoreIsAccessible(storeId.Value, accessibleStoreIds);
        }
    }

    private static void EnsureTransferIsAccessible(
        StockTransfer transfer,
        IReadOnlySet<int> accessibleStoreIds)
    {
        EnsureStoreIsAccessible(transfer.OriginStoreId, accessibleStoreIds);
        EnsureStoreIsAccessible(transfer.DestinationStoreId, accessibleStoreIds);
    }

    private static void EnsureStoreIsAccessible(int storeId, IReadOnlySet<int> accessibleStoreIds)
    {
        if (!accessibleStoreIds.Contains(storeId))
        {
            throw new OperationForbiddenException("O usuário não possui acesso a uma das lojas da transferência.");
        }
    }

    private static byte[] ParseRowVersion(string value)
    {
        try
        {
            var parsed = Convert.FromBase64String(value);
            return parsed.Length == 8 ? parsed : throw new FormatException();
        }
        catch (FormatException)
        {
            throw new RequestValidationException("rowVersion", "A versão de concorrência informada é inválida.");
        }
    }

    private static string NormalizeRequired(string? value, string field, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new RequestValidationException(field, $"Informe um valor com até {maximumLength} caracteres.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatNumber(long id) => $"TR-{id:000000}";

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "transfer", exception.Message);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private enum TransferAction
    {
        Dispatch,
        Receive,
        Cancel
    }
}
