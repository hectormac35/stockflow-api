namespace StockFlow.Api.Domain;

/// <summary>Se lanza cuando una operación incumple una regla de negocio (se traduce a HTTP 409).</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>Se lanza cuando un recurso no existe (se traduce a HTTP 404).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resource, object id)
        : base($"{resource} con id '{id}' no encontrado.") { }
}
