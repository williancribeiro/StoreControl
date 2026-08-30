using StoreControl.Domain.Sales;

namespace StoreControl.Domain.Cash;

public sealed class CashSession
{
    private const decimal MaximumMoney = 999_999_999_999.99m;
    private readonly List<CashMovement> _movements = [];

    private CashSession()
    {
    }

    public CashSession(
        Guid operationId,
        int cashRegisterId,
        Guid openedByUserId,
        decimal openingAmount,
        string correlationId)
    {
        OperationId = operationId != Guid.Empty
            ? operationId
            : throw new ArgumentException("Informe um identificador de operação válido.", nameof(operationId));
        CashRegisterId = cashRegisterId > 0
            ? cashRegisterId
            : throw new ArgumentException("Informe um caixa válido.", nameof(cashRegisterId));
        OpenedByUserId = openedByUserId != Guid.Empty
            ? openedByUserId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(openedByUserId));
        OpeningAmount = NormalizeMoney(openingAmount, false, nameof(openingAmount));
        ExpectedCashAmount = OpeningAmount;
        Status = CashSessionStatus.Open;
        OpenedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = OpenedAtUtc;

        if (OpeningAmount > 0)
        {
            _movements.Add(new CashMovement(
                this,
                CashMovementType.Opening,
                null,
                OpeningAmount,
                "Valor de abertura do caixa.",
                openedByUserId,
                correlationId,
                "CashSession",
                operationId.ToString("D"),
                null,
                null));
        }
    }

    public long Id { get; private set; }

    public Guid OperationId { get; private set; }

    public int CashRegisterId { get; private set; }

    public CashRegister CashRegister { get; private set; } = null!;

    public Guid OpenedByUserId { get; private set; }

    public DateTime OpenedAtUtc { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public CashSessionStatus Status { get; private set; }

    public decimal OpeningAmount { get; private set; }

    public decimal ExpectedCashAmount { get; private set; }

    public decimal? DeclaredClosingAmount { get; private set; }

    public decimal? DifferenceAmount { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<CashMovement> Movements => _movements.AsReadOnly();

    public CashMovement RecordManualMovement(
        Guid operationId,
        CashMovementType type,
        decimal amount,
        string reason,
        Guid userId,
        string correlationId)
    {
        EnsureOpen();
        if (type is not (CashMovementType.CashIn or CashMovementType.CashOut))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "O movimento manual deve ser uma entrada ou saída.");
        }

        var movement = new CashMovement(
            this,
            type,
            null,
            amount,
            reason,
            userId,
            correlationId,
            "CashManual",
            operationId.ToString("D"),
            operationId,
            null);
        ApplyExpectedCashChange(movement.Amount);
        _movements.Add(movement);
        UpdatedAtUtc = movement.OccurredAtUtc;
        return movement;
    }

    public CashMovement RecordSalePayment(
        SalePayment payment,
        Guid userId,
        string correlationId,
        Guid saleOperationId)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(payment);
        var movement = new CashMovement(
            this,
            CashMovementType.SalePayment,
            payment.Method,
            payment.Amount,
            "Pagamento de venda.",
            userId,
            correlationId,
            "Sale",
            saleOperationId.ToString("D"),
            null,
            payment);
        if (payment.Method == PaymentMethod.Cash)
        {
            ApplyExpectedCashChange(movement.Amount);
        }

        _movements.Add(movement);
        UpdatedAtUtc = movement.OccurredAtUtc;
        return movement;
    }

    public void Close(Guid userId, decimal declaredClosingAmount)
    {
        EnsureOpen();
        ClosedByUserId = userId != Guid.Empty
            ? userId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(userId));
        DeclaredClosingAmount = NormalizeMoney(declaredClosingAmount, false, nameof(declaredClosingAmount));
        DifferenceAmount = decimal.Round(
            DeclaredClosingAmount.Value - ExpectedCashAmount,
            2,
            MidpointRounding.AwayFromZero);
        Status = CashSessionStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = ClosedAtUtc.Value;
    }

    private void ApplyExpectedCashChange(decimal signedAmount)
    {
        var result = decimal.Round(ExpectedCashAmount + signedAmount, 2, MidpointRounding.AwayFromZero);
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"Saldo de caixa insuficiente. Disponível: {ExpectedCashAmount:0.00}; saída: {Math.Abs(signedAmount):0.00}.");
        }

        if (result > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(nameof(signedAmount), "O saldo esperado excede o limite permitido.");
        }

        ExpectedCashAmount = result;
    }

    private void EnsureOpen()
    {
        if (Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException("A sessão de caixa está fechada.");
        }
    }

    private static decimal NormalizeMoney(decimal value, bool mustBePositive, string parameterName)
    {
        var normalized = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (normalized < 0 || normalized > MaximumMoney || (mustBePositive && normalized == 0))
        {
            throw new ArgumentOutOfRangeException(parameterName, "O valor informado é inválido.");
        }

        return normalized;
    }
}
