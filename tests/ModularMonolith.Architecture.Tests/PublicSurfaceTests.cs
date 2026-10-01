using Admin.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Catalog.Modules;
using Orders.Modules;
using Reporting.Modules;

namespace ModularMonolith.Architecture.Tests;

// Module assemblies export only their composition surface. Each module's *.Contracts assembly is the
// place for types other modules may use, and is public by design, so it is not listed here.
public class PublicSurfaceTests
{
    public static TheoryData<Type, Type[]> ModulePublicTypes()
    {
        return new TheoryData<Type, Type[]>
        {
            { typeof(CatalogModule), [typeof(CatalogModule), typeof(CatalogModule.Modules)] },
            { typeof(OrdersModule), [typeof(OrdersModule), typeof(OrdersModule.Modules)] },
            { typeof(AdministrationModule), [typeof(AdministrationModule), typeof(AdministrationModule.Modules)] },
            { typeof(ReportingModule), [typeof(ReportingModule), typeof(ReportingModule.Modules)] },
            { typeof(IdentityModule), [typeof(IdentityModule), typeof(IdentityModule.Modules), typeof(IdentityAuthExtensions)] }
        };
    }

    [Theory]
    [MemberData(nameof(ModulePublicTypes))]
    public void Module_Should_Only_Expose_Composition_Surface(Type moduleType, Type[] expectedPublicTypes)
    {
        var assembly = moduleType.Assembly;

        var actual = assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = expectedPublicTypes
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
