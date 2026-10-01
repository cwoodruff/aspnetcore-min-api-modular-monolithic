using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;
using SharedKernel.Persistence.Entities;

namespace ModularMonolith.Api.Tests.Repositories;

[Collection(RepositoryDatabaseDefinition.Name)]
public class BaseRepositoryTests(RepositoryDatabaseFixture database)
{
    [Fact]
    public async Task EntityExists_ShouldReturnTrueForExistingAndFalseForMissing()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new GenreRepository(ctx);

        (await repo.EntityExists(1)).Should().BeTrue();
        (await repo.EntityExists(999)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_ShouldReturnSeededEntities()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new GenreRepository(ctx);

        var all = await repo.GetAll();
        all.Should().HaveCount(1);
        all[0].Name.Should().Be("Rock");
    }

    [Fact]
    public async Task GetById_ShouldReturnExistingEntity()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new GenreRepository(ctx);

        var entity = await repo.GetById(1);
        entity.Should().NotBeNull();
        entity!.Name.Should().Be("Rock");
    }

    [Fact]
    public async Task Add_Update_Delete_ShouldWork()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new GenreRepository(ctx);

        // Add
        var added = await repo.Add(new Genre { Name = "Jazz" });
        added.Id.Should().BeGreaterThan(0);
        (await repo.GetAll()).Should().HaveCount(2);

        // Update
        added.Name = "Smooth Jazz";
        var updated = await repo.Update(added);
        updated.Should().BeTrue();
        var reloaded = await repo.GetById(added.Id);
        reloaded!.Name.Should().Be("Smooth Jazz");

        // Delete
        var deleted = await repo.Delete(added.Id);
        deleted.Should().BeTrue();
        (await repo.EntityExists(added.Id)).Should().BeFalse();

        // Delete missing
        var deletedMissing = await repo.Delete(9999);
        deletedMissing.Should().BeFalse();
    }

    [Fact]
    public async Task GetByCondition_ShouldFilterEntities()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new GenreRepository(ctx);

        var query = repo.GetByCondition(g => g.Name == "Rock");
        query.Should().NotBeNull();
        query.ToList().Should().HaveCount(1);
    }
}
