using StoreControl.Domain.Parties;
using StoreControl.Domain.Stores;

namespace StoreControl.Domain.Sales;

public sealed class Sale
{
    private const decimal MaximumMoney = 999_999_999_999.99m;
    private readonly List<SaleItem> _items = [];
    private readonly List<SalePayment> _payments = [];

    private Sale()
    {
    }

    public Sale(
        Guid operationId,
        int storeId,
        long cashSessionId,
        int? customerId,
        Guid createdByUserId)
    {
        OperationId = operationId != Guid.Empty
            ? operationId
            : throw new ArgumentException("Informe um identificador de operação válido.", nameof(operationId));
        StoreId = storeId > 0
            ? storeId
            : throw new ArgumentException("Informe uma loja válida.", nameof(storeId));
        CashSessionId = cashSessionId > 0
            ? cashSessionId
            : throw new ArgumentException("Informe uma sessão de caixa válida.", nameof(cashSessionId));
        if (customerId <= 0)
        {
            throw new ArgumentException("Informe um cliente válido.", nameof(customerId));
        }

        CustomerId = customerId;
        CreatedByUserId = createdByUserId != Guid.Empty
            ? createdByUserId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(createdByUserId));
        Status = SaleStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public long Id { get; private set; }

    public Guid OperationId { get; private set; }

    public int StoreId { get; private set; }

    public Store Store { get; private set; } = null!;

    public long CashSessionId { get; private set; }

    public int? CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public SaleStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? FinalizedAtUtc { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<SalePayment> Payments => _payments.AsReadOnly();

    public void AddItem(
        int productId,
        string productCode,
        string productName,
        decimal quantity,
        decimal unitPrice)
    {
        EnsureDraft();
        if (_items.Any(item => item.ProductId == productId))
        {
            throw new InvalidOperationException($"O produto {productCode} já foi incluído na venda.");
        }

        var item = new SaleItem(this, productId, productCode, productName, quantity, unitPrice);
        var newSubtotal = decimal.Round(Subtotal + item.LineSubtotal, 2, MidpointRounding.AwayFromZero);
        if (newSubtotal > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "O subtotal da venda excede o limite permitido.");
        }

        _items.Add(item);
        Subtotal = newSubtotal;
    }

    public void FinalizeSale(
        decimal discountAmount,
        IReadOnlyCollection<(PaymentMethod Method, decimal Amount, string? Reference)> payments)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(payments);
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Inclua pelo menos um item antes de finalizar a venda.");
        }

        var normalizedDiscount = decimal.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
        if (normalizedDiscount < 0 || normalizedDiscount >= Subtotal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                "O desconto deve ser maior ou igual a zero e menor que o subtotal.");
        }

        if (payments.Count is < 1 or > 10)
        {
            throw new ArgumentException("Informe entre uma e dez formas de pagamento.", nameof(payments));
        }

        var normalizedPayments = payments
            .Select(payment => new SalePayment(this, payment.Method, payment.Amount, payment.Reference))
            .ToArray();
        var total = decimal.Round(Subtotal - normalizedDiscount, 2, MidpointRounding.AwayFromZero);
        var paid = normalizedPayments.Sum(payment => payment.Amount);
        if (paid != total)
        {
            throw new InvalidOperationException(
                $"A soma dos pagamentos ({paid:0.00}) deve ser igual ao total da venda ({total:0.00}).");
        }

        DiscountAmount = normalizedDiscount;
        TotalAmount = total;
        _payments.AddRange(normalizedPayments);
        Status = SaleStatus.Finalized;
        FinalizedAtUtc = DateTime.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Uma venda finalizada não pode ser alterada.");
        }
    }
}
