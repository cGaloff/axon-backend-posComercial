using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

public class TenantConfig
{
    public Guid Id { get; private set; }
    public string BusinessName { get; private set; } = string.Empty;
    public string? Nit { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }
    public string? LogoUrl { get; private set; }
    public bool IsResponsableIva { get; private set; }

    // Valor (en pesos) a partir del cual una merma reportada por el Bodeguero
    // queda pendiente de aprobación de un Administrador (Matriz de Roles y
    // Permisos v2, regla transversal B). Default seguro: 0 — hasta que un
    // Administrador lo suba explícitamente, TODA merma requiere aprobación; la
    // ausencia de configuración nunca se interpreta como "sin límite".
    public decimal MermaApprovalThreshold { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TenantConfig()
    {
    }

    public static TenantConfig Create(string businessName)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainException("El nombre del negocio es obligatorio.");
        }

        var now = DateTime.UtcNow;

        return new TenantConfig
        {
            Id = Guid.NewGuid(),
            BusinessName = businessName,
            IsResponsableIva = false,
            MermaApprovalThreshold = 0m,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void SetMermaApprovalThreshold(decimal threshold)
    {
        if (threshold < 0)
        {
            throw new DomainException("El umbral de aprobación de mermas no puede ser negativo.");
        }

        MermaApprovalThreshold = threshold;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string businessName,
        string? nit,
        string? address,
        string? phone,
        string? email,
        string? website,
        string? logoUrl,
        bool isResponsableIva)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainException("El nombre del negocio es obligatorio.");
        }

        BusinessName = businessName;
        Nit = nit;
        Address = address;
        Phone = phone;
        Email = email;
        Website = website;
        LogoUrl = logoUrl;
        IsResponsableIva = isResponsableIva;
        UpdatedAt = DateTime.UtcNow;
    }
}
