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

    // Datos comerciales del perfil del establecimiento (portal web). TaxRegime
    // es la versión en texto de IsResponsableIva: se mantienen sincronizados
    // desde ambos caminos de actualización (perfil y tenant-config).
    public string? LegalName { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string TaxRegime { get; private set; } = TaxRegimes.NoResponsableIva;

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
        TaxRegime = TaxRegimes.FromIsResponsableIva(isResponsableIva);
        UpdatedAt = DateTime.UtcNow;
    }

    // Email, sitio web y logo no forman parte del formulario de perfil: se
    // conservan como estén.
    public void UpdateProfile(
        string businessName,
        string? legalName,
        string? nit,
        string? phone,
        string? address,
        string? city,
        string? state,
        string taxRegime)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainException("El nombre del negocio es obligatorio.");
        }

        if (!TaxRegimes.IsValid(taxRegime))
        {
            throw new DomainException("Régimen tributario inválido.");
        }

        BusinessName = businessName;
        LegalName = legalName;
        Nit = nit;
        Phone = phone;
        Address = address;
        City = city;
        State = state;
        TaxRegime = taxRegime;
        IsResponsableIva = TaxRegimes.IsResponsableIva(taxRegime);
        UpdatedAt = DateTime.UtcNow;
    }
}

public static class TaxRegimes
{
    public const string ResponsableIva = "Responsable de IVA";
    public const string NoResponsableIva = "No responsable de IVA";

    public static readonly IReadOnlyList<string> All = new[] { ResponsableIva, NoResponsableIva };

    public static bool IsValid(string? taxRegime)
    {
        return taxRegime is not null && All.Contains(taxRegime);
    }

    public static bool IsResponsableIva(string taxRegime)
    {
        return taxRegime == ResponsableIva;
    }

    public static string FromIsResponsableIva(bool isResponsableIva)
    {
        return isResponsableIva ? ResponsableIva : NoResponsableIva;
    }
}
