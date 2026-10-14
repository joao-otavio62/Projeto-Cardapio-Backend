using FluentValidation;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.ValueObjects;

namespace PizzaFlow.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(160);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}

public class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(x => x.ZipCode).Must(z => Cpf.Normalize(z).Length == 8).WithMessage("CEP inválido.");
        RuleFor(x => x.Street).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Number).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Complement).MaximumLength(80);
        RuleFor(x => x.District).NotEmpty().MaximumLength(80);
        RuleFor(x => x.City).NotEmpty().MaximumLength(80);
        RuleFor(x => x.State).NotEmpty().Length(2).WithMessage("UF deve ter 2 letras.");
    }
}

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(3).MaximumLength(160);
        RuleFor(x => x.Cpf).Must(Cpf.IsValid).WithMessage("CPF inválido.");
        RuleFor(x => x.Phone)
            .Must(p => Cpf.Normalize(p).Length is 10 or 11).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Telefone inválido.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(160).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.DeliveryType).IsInEnum();
        RuleFor(x => x.PaymentMethod).IsInEnum();

        When(x => x.DeliveryType == DeliveryType.Delivery, () =>
            RuleFor(x => x.Address!).NotNull().WithMessage("Endereço é obrigatório para entrega.")
                .SetValidator(new AddressRequestValidator()));

        RuleFor(x => x.Items).NotEmpty().WithMessage("O carrinho está vazio.")
            .Must(i => i.Count <= 50).WithMessage("Máximo de 50 itens por pedido.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(y => y.ProductId).NotEmpty();
            i.RuleFor(y => y.Quantity).InclusiveBetween(1, 50);
            i.RuleFor(y => y.Notes).MaximumLength(255);
        });

        When(x => x.PaymentMethod == PaymentMethod.CreditCard, () =>
            RuleFor(x => x.Card!).NotNull().WithMessage("Dados do cartão (token) são obrigatórios.")
                .ChildRules(c =>
                {
                    c.RuleFor(y => y.Token).NotEmpty().MaximumLength(200);
                    c.RuleFor(y => y.PaymentMethodId).NotEmpty().MaximumLength(40);
                    c.RuleFor(y => y.Installments).InclusiveBetween(1, 12);
                }));
    }
}

public class OrderLookupRequestValidator : AbstractValidator<OrderLookupRequest>
{
    public OrderLookupRequestValidator() =>
        RuleFor(x => x.Cpf).Must(Cpf.IsValid).WithMessage("CPF inválido.");
}

public class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Messages).NotEmpty().Must(m => m.Count <= 30).WithMessage("Histórico muito longo.")
            .Must(m => m.Count > 0 && m[^1].Role == "user").WithMessage("A última mensagem deve ser do usuário.");
        RuleForEach(x => x.Messages).ChildRules(m =>
        {
            m.RuleFor(y => y.Role).Must(r => r is "user" or "assistant");
            m.RuleFor(y => y.Content).NotEmpty().MaximumLength(1000);
        });
    }
}

public class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(140);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, false);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockAlert).GreaterThanOrEqualTo(0);
    }
}

public class StockAdjustRequestValidator : AbstractValidator<StockAdjustRequest>
{
    public StockAdjustRequestValidator() =>
        RuleFor(x => x.Quantity).NotEqual(0).InclusiveBetween(-100000, 100000);
}

public class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
}

public class PromotionRequestValidator : AbstractValidator<PromotionRequest>
{
    public PromotionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.DiscountValue).GreaterThan(0).PrecisionScale(12, 2, false);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percent)
            .WithMessage("Desconto percentual máximo é 100.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("O fim deve ser posterior ao início.");
    }
}

public class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator() =>
        RuleFor(x => x.Status).IsInEnum().NotEqual(OrderStatus.PendingPayment);
}

public class UpdateStoreRequestValidator : AbstractValidator<UpdateStoreRequest>
{
    public UpdateStoreRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.DeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinOrder).GreaterThanOrEqualTo(0);
    }
}
