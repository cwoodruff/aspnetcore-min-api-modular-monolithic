using System.Reflection;
using System.Runtime.CompilerServices;
using Admin.Modules;
using Catalog.Modules;
using Identity.Modules;
using Orders.Modules;
using Reporting.Modules;

namespace ModularMonolith.Architecture.Tests;

public class GuardrailTests
{
    public static TheoryData<Type> ModuleTypes() =>
    [
        typeof(CatalogModule),
        typeof(OrdersModule),
        typeof(AdministrationModule),
        typeof(IdentityModule),
        typeof(ReportingModule)
    ];

    [Theory]
    [MemberData(nameof(ModuleTypes))]
    public void Module_Has_At_Most_One_InternalsVisibleTo_And_It_Is_Its_Allowed_Test_Project(Type moduleType)
    {
        var moduleAssembly = moduleType.Assembly;
        var moduleName = moduleAssembly.GetName().Name!;
        Assert.True(
            ArchitectureConstants.AllowedInternalsVisibleTo.TryGetValue(moduleName, out var allowed),
            $"{moduleName} has no entry in ArchitectureConstants.AllowedInternalsVisibleTo.");

        var granted = moduleAssembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        if (allowed is null)
        {
            Assert.Empty(granted);
        }
        else
        {
            Assert.True(granted.Length <= 1, $"{moduleName} grants InternalsVisibleTo to: {string.Join(", ", granted)}.");
            Assert.All(granted, name => Assert.Equal(allowed, name));
        }
    }
}
