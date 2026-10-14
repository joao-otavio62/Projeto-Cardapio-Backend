namespace PizzaFlow.Domain.Exceptions;

/// <summary>Violação de regra de negócio -> HTTP 400.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Recurso inexistente (ou de outra loja) -> HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Conflito de estado/concorrência -> HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Credenciais inválidas / não autenticado -> HTTP 401.</summary>
public class AuthenticationFailedException(string message) : Exception(message);

/// <summary>Falha no gateway de pagamento -> HTTP 502.</summary>
public class PaymentGatewayException(string message) : Exception(message);
