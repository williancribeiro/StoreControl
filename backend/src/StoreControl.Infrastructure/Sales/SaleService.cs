using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Sales;
using StoreControl.Domain.Cash;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Sales;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Persistence;
using StoreControl.Infrastructure.Messaging;

namespace StoreControl.Infrastructure.Sales;

internal sealed class SaleService(
    StoreControlDbContext dbContext,
    ILogger<SaleService> logger) : ISaleService
{
    private static readonly string[] AllowedSorts = ["finalizedAt", "total", "customer", "store"];

    public async Task<PagedResult<SaleSummary>> ListAsync(
        Guid userId,
        SaleListQuery query,
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
        EnsureRequestedStoreIsAccessible(query.StoreId, accessibleStoreIds);
        var sales = dbContext.Sales
            .AsNoTracking()
            .Where(sale => accessibleStoreIds.Contains(sale.StoreId));
        if (query.StoreId.HasValue)
        {
            sales = sales.Where(sale => sale.StoreId == query.StoreId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            sales = sales.Where(sale => sale.CustomerId == query.CustomerId.Value);
        }

        if (query.PaymentMethod.HasValue)
        {
            sales = sales.Where(sale => sale.Payments.Any(payment => payment.Method == query.PaymentMethod.Value));
        }

        if (query.FromUtc.HasValue)
        {
            sales = sales.Where(sale => sale.FinalizedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            sales = sales.Where(sale => sale.FinalizedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var numericSearch = long.TryParse(search.TrimStart('#'), out var parsedId) ? parsedId : (long?)null;
            sales = sales.Where(sale =>
                (numericSearch.HasValue && sale.Id == numericSearch.Value) ||
                sale.Store.Code.Contains(search) ||
                (sale.Customer != null &&
                    (sale.Customer.Code.Contains(search) || sale.Customer.Name.Contains(search))) ||
                sale.Items.Any(item =>
                    item.ProductCode.Contains(search) || item.ProductName.Contains(search)));
        }

        var totalCount = await sales.CountAsync(cancellationToken);
        sales = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("total", false) => sales.OrderBy(sale => sale.TotalAmount).ThenBy(sale => sale.Id),
            ("total", true) => sales.OrderByDescending(sale => sale.TotalAmount).ThenByDescending(sale => sale.Id),
            ("customer", false) => sales.OrderBy(sale => sale.Customer == null ? string.Empty : sale.Customer.Name).ThenByDescending(sale => sale.Id),
            ("customer", true) => sales.OrderByDescending(sale => sale.Customer == null ? string.Empty : sale.Customer.Name).ThenByDescending(sale => sale.Id),
            ("store", false) => sales.OrderBy(sale => sale.Store.Name).ThenByDescending(sale => sale.Id),
            ("store", true) => sales.OrderByDescending(sale => sale.Store.Name).ThenByDescending(sale => sale.Id),
            ("finalizedat", false) => sales.OrderBy(sale => sale.FinalizedAtUtc),
            _ => sales.OrderByDescending(sale => sale.FinalizedAtUtc)
        };

        var items = await sales
            .Include(sale => sale.Store)
            .Include(sale => sale.Customer)
            .Include(sale => sale.Items)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var userIds = items.Select(item => item.CreatedByUserId).Distinct().ToArray();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return new PagedResult<SaleSummary>(
            items.Select(item => MapSummary(item, userNames[item.CreatedByUserId])).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<SaleDetails> GetAsync(
        Guid userId,
        long saleId,
        CancellationToken cancellationToken)
    {
        var sale = await DetailsQuery(false)
            .SingleOrDefaultAsync(item => item.Id == saleId, cancellationToken)
            ?? throw new ResourceNotFoundException("Venda não encontrada.");
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureStoreIsAccessible(sale.StoreId, accessibleStoreIds);
        return await MapDetailsAsync(sale, cancellationToken);
    }

    public async Task<FinalizeSaleResult> FinalizeAsync(
        Guid userId,
        string correlationId,
        FinalizeSaleCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command);
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureStoreIsAccessible(command.StoreId, accessibleStoreIds);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var executionResult = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var replay = await DetailsQuery(false)
                    .SingleOrDefaultAsync(sale => sale.OperationId == command.OperationId, cancellationToken);
                if (replay is not null)
                {
                    EnsureReplayMatches(replay, userId, command);
                    await transaction.CommitAsync(cancellationToken);
                    return (replay.Id, true);
                }

                var cashSession = await LockedCashSessionQuery(command.CashSessionId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new RequestValidationException("cashSessionId", "A sessão de caixa informada não existe.");
                EnsureStoreIsAccessible(cashSession.CashRegister.StoreId, accessibleStoreIds);
                if (cashSession.CashRegister.StoreId != command.StoreId)
                {
                    throw new RequestValidationException(
                        "cashSessionId",
                        "A sessão de caixa deve pertencer à mesma loja da venda.");
                }

                if (!cashSession.CashRegister.IsActive || !cashSession.CashRegister.Store.IsActive)
                {
                    throw new RequestValidationException("cashSessionId", "O caixa e sua loja devem estar ativos.");
                }

                if (cashSession.Status != CashSessionStatus.Open)
                {
                    throw new RequestValidationException("cashSessionId", "A sessão de caixa informada está fechada.");
                }

                replay = await DetailsQuery(false)
                    .SingleOrDefaultAsync(sale => sale.OperationId == command.OperationId, cancellationToken);
                if (replay is not null)
                {
                    EnsureReplayMatches(replay, userId, command);
                    await transaction.CommitAsync(cancellationToken);
                    return (replay.Id, true);
                }

                if (command.CustomerId.HasValue)
                {
                    var customerIsActive = await dbContext.Customers
                        .AsNoTracking()
                        .Where(customer => customer.Id == command.CustomerId.Value)
                        .Select(customer => (bool?)customer.IsActive)
                        .SingleOrDefaultAsync(cancellationToken);
                    if (!customerIsActive.HasValue)
                    {
                        throw new RequestValidationException("customerId", "O cliente informado não existe.");
                    }

                    if (!customerIsActive.Value)
                    {
                        throw new RequestValidationException("customerId", "O cliente informado está inativo.");
                    }
                }

                var productIds = command.Items.Select(item => item.ProductId).OrderBy(item => item).ToArray();
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
                    throw new RequestValidationException("items", "Produtos inativos não podem ser vendidos.");
                }

                var balances = await dbContext.StockBalances
                    .Where(balance => balance.StoreId == command.StoreId && productIds.Contains(balance.ProductId))
                    .OrderBy(balance => balance.ProductId)
                    .ToDictionaryAsync(balance => balance.ProductId, cancellationToken);

                var sale = new Sale(
                    command.OperationId,
                    command.StoreId,
                    command.CashSessionId,
                    command.CustomerId,
                    userId);
                foreach (var input in command.Items)
                {
                    var product = products[input.ProductId];
                    sale.AddItem(
                        product.Id,
                        product.Code,
                        product.Name,
                        input.Quantity,
                        product.SalePrice);
                }

                sale.FinalizeSale(
                    command.DiscountAmount,
                    command.Payments
                        .Select(payment => (payment.Method, payment.Amount, payment.Reference))
                        .ToArray());

                foreach (var item in sale.Items.OrderBy(item => item.ProductId))
                {
                    if (!balances.TryGetValue(item.ProductId, out var balance))
                    {
                        throw new ResourceConflictException(
                            $"Estoque insuficiente para o produto {item.ProductCode}. Disponível: 0; solicitado: {item.Quantity:0.###}.");
                    }

                    dbContext.StockMovements.Add(balance.ApplyMovement(
                        StockMovementType.Sale,
                        item.Quantity,
                        $"Saída da venda {command.OperationId:D}.",
                        userId,
                        correlationId,
                        "Sale",
                        command.OperationId.ToString("D")));
                }

                foreach (var payment in sale.Payments)
                {
                    cashSession.RecordSalePayment(payment, userId, correlationId, command.OperationId);
                }

                dbContext.Sales.Add(sale);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.OutboxMessages.Add(MessagingContracts.CreateSaleFinalized(sale, correlationId));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation(
                    "Sale {SaleId} finalized for store {StoreId} with total {TotalAmount}",
                    sale.Id,
                    sale.StoreId,
                    sale.TotalAmount);
                return (sale.Id, false);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "O estoque ou o caixa foi alterado por outra operação. Recarregue os dados antes de tentar novamente.");
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                var replay = await DetailsQuery(false)
                    .SingleOrDefaultAsync(sale => sale.OperationId == command.OperationId, cancellationToken);
                if (replay is not null)
                {
                    EnsureReplayMatches(replay, userId, command);
                    return (replay.Id, true);
                }

                throw new ResourceConflictException(
                    "A venda não foi concluída por um conflito de persistência. Nenhuma alteração foi aplicada.");
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

        return new FinalizeSaleResult(
            await GetAsync(userId, executionResult.Id, cancellationToken),
            executionResult.Item2);
    }

    public async Task<CancelSaleResult> CancelAsync(
        Guid userId,
        string correlationId,
        long saleId,
        CancelSaleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        var expectedRowVersion = ParseRowVersion(command.RowVersion);
        var normalizedReason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        if (normalizedReason is null || normalizedReason.Length > 500)
        {
            throw new RequestValidationException("reason", "Informe um motivo com até 500 caracteres.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var operationReplay = await DetailsQuery(false)
                    .SingleOrDefaultAsync(
                        sale => sale.CancellationOperationId == command.OperationId,
                        cancellationToken);
                if (operationReplay is not null)
                {
                    EnsureCancellationReplayMatches(operationReplay, saleId, userId, command, normalizedReason);
                    EnsureStoreIsAccessible(operationReplay.StoreId, accessibleStoreIds);
                    await transaction.CommitAsync(cancellationToken);
                    return (operationReplay.Id, true);
                }

                var sale = await LockedSaleQuery(saleId).SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ResourceNotFoundException("Venda não encontrada.");
                EnsureStoreIsAccessible(sale.StoreId, accessibleStoreIds);
                if (sale.Status == SaleStatus.Cancelled)
                {
                    throw new ResourceConflictException("A venda já está cancelada.");
                }

                if (!sale.RowVersion.SequenceEqual(expectedRowVersion))
                {
                    throw new ResourceConflictException(
                        "A venda foi alterada por outra operação. Recarregue os dados antes de tentar novamente.");
                }

                var cashSession = await LockedCashSessionQuery(command.CashSessionId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new RequestValidationException("cashSessionId", "A sessão de caixa informada não existe.");
                EnsureStoreIsAccessible(cashSession.CashRegister.StoreId, accessibleStoreIds);
                if (cashSession.CashRegister.StoreId != sale.StoreId)
                {
                    throw new RequestValidationException(
                        "cashSessionId",
                        "A sessão de caixa do estorno deve pertencer à loja da venda.");
                }

                if (!cashSession.CashRegister.IsActive || !cashSession.CashRegister.Store.IsActive)
                {
                    throw new RequestValidationException("cashSessionId", "O caixa e sua loja devem estar ativos.");
                }

                var productIds = sale.Items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
                var balances = await dbContext.StockBalances
                    .Where(balance => balance.StoreId == sale.StoreId && productIds.Contains(balance.ProductId))
                    .OrderBy(balance => balance.ProductId)
                    .ToDictionaryAsync(balance => balance.ProductId, cancellationToken);
                if (balances.Count != productIds.Length)
                {
                    throw new ResourceConflictException(
                        "A posição de estoque original da venda não está disponível para o estorno.");
                }

                foreach (var item in sale.Items.OrderBy(item => item.ProductId))
                {
                    dbContext.StockMovements.Add(balances[item.ProductId].ApplyMovement(
                        StockMovementType.SaleReversal,
                        item.Quantity,
                        $"Devolução do cancelamento da venda {sale.Id}.",
                        userId,
                        correlationId,
                        "SaleCancellation",
                        command.OperationId.ToString("D")));
                }

                foreach (var payment in sale.Payments.OrderBy(payment => payment.Id))
                {
                    cashSession.RecordSaleRefund(payment, userId, correlationId, command.OperationId);
                }

                sale.Cancel(command.OperationId, command.CashSessionId, userId, normalizedReason);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation(
                    "Sale {SaleId} cancelled in store {StoreId} by user {UserId}",
                    sale.Id,
                    sale.StoreId,
                    userId);
                return (sale.Id, false);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "A venda, o estoque ou o caixa foi alterado por outra operação. Recarregue os dados.");
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                var replay = await DetailsQuery(false)
                    .SingleOrDefaultAsync(
                        sale => sale.CancellationOperationId == command.OperationId,
                        cancellationToken);
                if (replay is not null)
                {
                    EnsureCancellationReplayMatches(replay, saleId, userId, command, normalizedReason);
                    return (replay.Id, true);
                }

                throw new ResourceConflictException(
                    "O cancelamento não foi concluído por um conflito de persistência. Nenhuma alteração foi aplicada.");
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

        return new CancelSaleResult(
            await GetAsync(userId, result.Id, cancellationToken),
            result.Item2);
    }

    private IQueryable<Sale> DetailsQuery(bool tracking)
    {
        var query = dbContext.Sales
            .Include(sale => sale.Store)
            .Include(sale => sale.Customer)
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .Include(sale => sale.StatusHistory)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<CashSession> LockedCashSessionQuery(long cashSessionId) =>
        dbContext.CashSessions
            .FromSqlInterpolated($"""
                SELECT *
                FROM [cash].[CashSessions] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = {cashSessionId}
                """)
            .Include(session => session.CashRegister)
                .ThenInclude(register => register.Store);

    private IQueryable<Sale> LockedSaleQuery(long saleId) =>
        dbContext.Sales
            .FromSqlInterpolated($"""
                SELECT *
                FROM [sales].[Sales] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = {saleId}
                """)
            .Include(sale => sale.Store)
            .Include(sale => sale.Customer)
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .Include(sale => sale.StatusHistory)
            .AsSplitQuery();

    private async Task<SaleDetails> MapDetailsAsync(Sale sale, CancellationToken cancellationToken)
    {
        var relatedUserIds = sale.StatusHistory
            .Select(history => history.ChangedByUserId)
            .Append(sale.CreatedByUserId)
            .Concat(sale.CancelledByUserId.HasValue ? [sale.CancelledByUserId.Value] : [])
            .Distinct()
            .ToArray();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => relatedUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        var cashRegisterCode = await dbContext.CashSessions
            .AsNoTracking()
            .Where(session => session.Id == sale.CashSessionId)
            .Select(session => session.CashRegister.Code)
            .SingleAsync(cancellationToken);
        var cancellationCashRegisterCode = sale.CancellationCashSessionId.HasValue
            ? await dbContext.CashSessions
                .AsNoTracking()
                .Where(session => session.Id == sale.CancellationCashSessionId.Value)
                .Select(session => session.CashRegister.Code)
                .SingleAsync(cancellationToken)
            : null;

        return new SaleDetails(
            sale.Id,
            FormatNumber(sale.Id),
            sale.OperationId,
            sale.StoreId,
            sale.Store.Code,
            sale.Store.Name,
            sale.CustomerId,
            sale.Customer?.Code,
            sale.Customer?.Name,
            sale.CreatedByUserId,
            userNames[sale.CreatedByUserId],
            sale.CashSessionId,
            cashRegisterCode,
            sale.Status.ToString(),
            sale.CreatedAtUtc,
            sale.FinalizedAtUtc!.Value,
            sale.CancellationOperationId,
            sale.CancellationCashSessionId,
            cancellationCashRegisterCode,
            sale.CancelledByUserId,
            sale.CancelledByUserId.HasValue ? userNames[sale.CancelledByUserId.Value] : null,
            sale.CancelledAtUtc,
            sale.CancellationReason,
            Convert.ToBase64String(sale.RowVersion),
            sale.Subtotal,
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.Items
                .OrderBy(item => item.Id)
                .Select(item => new SaleItemSummary(
                    item.Id,
                    item.ProductId,
                    item.ProductCode,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.LineSubtotal))
                .ToArray(),
            sale.Payments
                .OrderBy(payment => payment.Id)
                .Select(payment => new SalePaymentSummary(
                    payment.Id,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Reference))
                .ToArray(),
            sale.StatusHistory
                .OrderBy(history => history.ChangedAtUtc)
                .ThenBy(history => history.Id)
                .Select(history => new SaleStatusHistorySummary(
                    history.Id,
                    history.Status.ToString(),
                    history.ChangedByUserId,
                    userNames[history.ChangedByUserId],
                    history.ChangedAtUtc,
                    history.Description))
                .ToArray());
    }

    private static SaleSummary MapSummary(Sale sale, string userName) =>
        new(
            sale.Id,
            FormatNumber(sale.Id),
            sale.OperationId,
            sale.StoreId,
            sale.Store.Code,
            sale.Store.Name,
            sale.CustomerId,
            sale.Customer?.Code,
            sale.Customer?.Name,
            sale.CreatedByUserId,
            userName,
            sale.CashSessionId,
            sale.Status.ToString(),
            sale.Items.Count,
            sale.Subtotal,
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.FinalizedAtUtc!.Value);

    private static void ValidateCommand(FinalizeSaleCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        if (command.Items.Count is < 1 or > 100)
        {
            throw new RequestValidationException("items", "Informe entre 1 e 100 itens na venda.");
        }

        if (command.Items.Select(item => item.ProductId).Distinct().Count() != command.Items.Count)
        {
            throw new RequestValidationException("items", "Um produto não pode aparecer mais de uma vez na venda.");
        }

        if (command.Payments.Count is < 1 or > 10)
        {
            throw new RequestValidationException("payments", "Informe entre uma e dez formas de pagamento.");
        }
    }

    private static void EnsureReplayMatches(Sale sale, Guid userId, FinalizeSaleCommand command)
    {
        var itemsMatch = sale.Items.Count == command.Items.Count && command.Items.All(input =>
            sale.Items.Any(item =>
                item.ProductId == input.ProductId &&
                item.Quantity == decimal.Round(input.Quantity, 3, MidpointRounding.AwayFromZero)));
        var existingPayments = sale.Payments
            .Select(payment => (payment.Method, payment.Amount, Reference: NormalizeReference(payment.Reference)))
            .OrderBy(payment => payment.Method)
            .ThenBy(payment => payment.Amount)
            .ThenBy(payment => payment.Reference)
            .ToArray();
        var requestedPayments = command.Payments
            .Select(payment => (
                payment.Method,
                Amount: decimal.Round(payment.Amount, 2, MidpointRounding.AwayFromZero),
                Reference: NormalizeReference(payment.Reference)))
            .OrderBy(payment => payment.Method)
            .ThenBy(payment => payment.Amount)
            .ThenBy(payment => payment.Reference)
            .ToArray();
        if (sale.CreatedByUserId != userId ||
            sale.StoreId != command.StoreId ||
            sale.CashSessionId != command.CashSessionId ||
            sale.CustomerId != command.CustomerId ||
            sale.DiscountAmount != decimal.Round(command.DiscountAmount, 2, MidpointRounding.AwayFromZero) ||
            !itemsMatch ||
            !existingPayments.SequenceEqual(requestedPayments))
        {
            throw new ResourceConflictException("A chave de idempotência já foi usada com outra venda.");
        }
    }

    private static void EnsureCancellationReplayMatches(
        Sale sale,
        long saleId,
        Guid userId,
        CancelSaleCommand command,
        string normalizedReason)
    {
        if (sale.Id != saleId ||
            sale.CancelledByUserId != userId ||
            sale.CancellationCashSessionId != command.CashSessionId ||
            !string.Equals(sale.CancellationReason, normalizedReason, StringComparison.Ordinal))
        {
            throw new ResourceConflictException("A chave de idempotência já foi usada em outro cancelamento.");
        }
    }

    private static byte[] ParseRowVersion(string value)
    {
        try
        {
            var parsed = Convert.FromBase64String(value);
            return parsed.Length == 8
                ? parsed
                : throw new FormatException();
        }
        catch (FormatException)
        {
            throw new RequestValidationException("rowVersion", "A versão de concorrência informada é inválida.");
        }
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

    private Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == RoleNames.Administrator
         select userRole).AnyAsync(cancellationToken);

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

    private static string? NormalizeReference(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatNumber(long id) => $"VD-{id:000000}";

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "sale", exception.Message);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
