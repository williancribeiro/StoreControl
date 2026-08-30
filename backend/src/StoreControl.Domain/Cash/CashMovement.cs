using StoreControl.Domain.Sales;

namespace StoreControl.Domain.Cash;

public sealed class CashMovement
{
    private CashMovement()
    {
    }

    internal CashMovement(
        CashSession session,
        CashMovementType type,
        PaymentMethod? paymentMethod,
        decimal amount,
        string reason,
        Guid createdByUserId,
        string correlationId,
        string? referenceType,
        string? referenceId,
        Guid? operationId,
        SalePayment? salePayment)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "O tipo de movimentação é inválido.");
        }

        if ((type == CashMovementType.SalePayment) != paymentMethod.HasValue ||
            (type == CashMovementType.SalePayment) != (salePayment is not null))
        {
            throw new ArgumentException(
                "Forma de pagamento e pagamento de venda são obrigatórios somente para movimentos de venda.",
                nameof(paymentMethod));
        }

        if (paymentMethod.HasValue && !Enum.IsDefined(paymentMethod.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(paymentMethod), "A forma de pagamento é inválida.");
        }

        Type = type;
        PaymentMethod = paymentMethod;
        Amount = NormalizeSignedAmount(type, amount);
        Reason = NormalizeRequired(reason, 300, nameof(reason));
        CreatedByUserId = createdByUserId != Guid.Empty
            ? createdByUserId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(createdByUserId));
        CorrelationId = NormalizeRequired(correlationId, 128, nameof(correlationId));
        ReferenceType = NormalizeOptional(referenceType, 50, nameof(referenceType));
        ReferenceId = NormalizeOptional(referenceId, 100, nameof(referenceId));
        if ((ReferenceType is null) != (ReferenceId is null))
        {
            throw new ArgumentException("Tipo e identificador da referência devem ser informados juntos.", nameof(referenceId));
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Informe um identificador de operação válido.", nameof(operationId));
        }

        OperationId = operationId;
        SalePayment = salePayment;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public long Id { get; private set; }

    public long CashSessionId { get; private set; }

    public CashSession Session { get; private set; } = null!;

    public CashMovementType Type { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string? ReferenceType { get; private set; }

    public string? ReferenceId { get; private set; }

    public Guid? OperationId { get; private set; }

    public long? SalePaymentId { get; private set; }

    public SalePayment? SalePayment { get; private set; }

    private static decimal NormalizeSignedAmount(CashMovementType type, decimal amount)
    {
        var normalized = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (normalized <= 0 || normalized > 999_999_999_999.99m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "O valor deve estar entre 0,01 e 999.999.999.999,99.");
        }

        return type == CashMovementType.CashOut ? -normalized : normalized;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"O valor deve possuir entre 1 e {maximumLength} caracteres.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new ArgumentException($"O valor deve possuir no máximo {maximumLength} caracteres.", parameterName);
        }

        return normalized;
    }
}
