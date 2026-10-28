using Axon.Application.TenantConfig.Commands;
using Axon.Application.TenantConfig.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.TenantConfig;

public class TenantProfileTests
{
    private static UpdateTenantProfileCommand ValidCommand(string businessName = "Supermercado Don Pedro Centro", string taxRegime = TaxRegimes.ResponsableIva) =>
        new(businessName, "Inversiones Don Pedro S.A.S.", "901.784.521-3", "+57 301 481 0079",
            "Calle 72 # 48 - 25", "Barranquilla", "Atlántico", taxRegime);

    [Fact]
    public async Task GetProfile_CombinesTenantConfigMasterTenantAndOwner()
    {
        var dbContext = TestDbContextFactory.Create();
        var masterDbContext = TestMasterDbContextFactory.Create();

        var tenant = Tenant.Create("test-tenant", "Mi Tienda", "basic", "owner@test.com");
        masterDbContext.Tenants.Add(tenant);
        await masterDbContext.SaveChangesAsync();

        var ownerRole = Role.Create(Role.OwnerRoleName, "", isSystem: true);
        dbContext.Roles.Add(ownerRole);
        dbContext.Users.Add(User.Create("Juan Pérez", "owner@test.com", "hash", ownerRole.Id));
        await dbContext.SaveChangesAsync();

        var config = TenantConfigEntity.Create("Mi Tienda");
        config.UpdateProfile("Mi Tienda", "Mi Tienda S.A.S.", "900", null, null, "Bogotá", "Cundinamarca", TaxRegimes.ResponsableIva);

        var handler = new GetTenantProfileQueryHandler(
            new FakeTenantConfigRepository(config), dbContext, masterDbContext, new FakeTenantContext());

        var result = await handler.Handle(new GetTenantProfileQuery(), CancellationToken.None);

        Assert.Equal(tenant.Id, result.Id);
        Assert.Equal("test-tenant", result.Slug);
        Assert.Equal("Mi Tienda S.A.S.", result.LegalName);
        Assert.Equal("Bogotá", result.City);
        Assert.Equal(TaxRegimes.ResponsableIva, result.TaxRegime);
        Assert.Equal("owner@test.com", result.Email);
        Assert.Equal("Juan Pérez", result.OwnerName);
    }

    [Fact]
    public async Task UpdateProfile_SavesFieldsSyncsIvaFlagAndRenamesMasterTenant()
    {
        var dbContext = TestDbContextFactory.Create();
        var masterDbContext = TestMasterDbContextFactory.Create();
        masterDbContext.Tenants.Add(Tenant.Create("test-tenant", "Mi Tienda", "basic", "owner@test.com"));
        await masterDbContext.SaveChangesAsync();

        var config = TenantConfigEntity.Create("Mi Tienda");
        var invalidator = new FakeTenantCacheInvalidator();
        var handler = new UpdateTenantProfileCommandHandler(
            new FakeTenantConfigRepository(config), new FakeUnitOfWork(dbContext), masterDbContext,
            new FakeTenantContext(), invalidator);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Supermercado Don Pedro Centro", result.BusinessName);
        Assert.Equal("Barranquilla", config.City);
        Assert.True(config.IsResponsableIva);
        Assert.Equal("Supermercado Don Pedro Centro", masterDbContext.Tenants.Single().BusinessName);
        Assert.Contains("test-tenant", invalidator.InvalidatedSlugs);
    }

    [Fact]
    public void LegacyConfigUpdate_KeepsTaxRegimeInSyncWithIvaFlag()
    {
        var config = TenantConfigEntity.Create("Mi Tienda");

        config.Update("Mi Tienda", null, null, null, null, null, null, isResponsableIva: true);

        Assert.Equal(TaxRegimes.ResponsableIva, config.TaxRegime);
    }

    [Fact]
    public void UpdateProfile_WithUnknownTaxRegime_Throws()
    {
        var config = TenantConfigEntity.Create("Mi Tienda");

        Assert.Throws<DomainException>(
            () => config.UpdateProfile("Mi Tienda", "X", null, null, null, null, null, "Régimen inventado"));
    }

    [Fact]
    public void Validator_RequiresLegalNameAndKnownTaxRegime()
    {
        var validator = new UpdateTenantProfileCommandValidator();

        Assert.True(validator.Validate(ValidCommand()).IsValid);
        Assert.False(validator.Validate(ValidCommand() with { LegalName = "" }).IsValid);
        Assert.False(validator.Validate(ValidCommand(taxRegime: "Otro")).IsValid);
    }
}
