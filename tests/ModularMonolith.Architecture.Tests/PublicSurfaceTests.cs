using Admin.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Music.Modules;
using Orders.Modules;
using Reporting.Modules;

namespace ModularMonolith.Architecture.Tests;

public class PublicSurfaceTests
{
    public static TheoryData<Type, Type[]> ModulePublicTypes()
    {
        return new TheoryData<Type, Type[]>
        {
            { typeof(MusicModule), [typeof(MusicModule), typeof(MusicModule.Modules)] },
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
