using Admin.Modules.Domain;
using Admin.Modules.Models;

namespace Admin.Modules.Mapping;

/// <summary>Entity and API model conversions for the Administration module.</summary>
internal static class AdministrationMappings
{
    public static CustomerApiModel ToApiModel(this Customer entity) => new()
    {
        Id = entity.Id,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        Company = entity.Company,
        Address = entity.Address,
        City = entity.City,
        State = entity.State,
        Country = entity.Country,
        PostalCode = entity.PostalCode,
        Phone = entity.Phone,
        Fax = entity.Fax,
        Email = entity.Email
    };

    public static Customer ToEntity(this CustomerApiModel model) => new()
    {
        Id = model.Id,
        FirstName = model.FirstName,
        LastName = model.LastName,
        Company = model.Company,
        Address = model.Address,
        City = model.City,
        State = model.State,
        Country = model.Country,
        PostalCode = model.PostalCode,
        Phone = model.Phone,
        Fax = model.Fax,
        Email = model.Email,
        SupportRepId = model.SupportRepId
    };

    public static EmployeeApiModel ToApiModel(this Employee entity) => new()
    {
        Id = entity.Id,
        LastName = entity.LastName,
        FirstName = entity.FirstName,
        Title = entity.Title,
        ReportsTo = entity.ReportsTo,
        BirthDate = entity.BirthDate,
        HireDate = entity.HireDate,
        Address = entity.Address,
        City = entity.City,
        State = entity.State,
        Country = entity.Country,
        PostalCode = entity.PostalCode,
        Phone = entity.Phone,
        Fax = entity.Fax,
        Email = entity.Email
    };

    public static Employee ToEntity(this EmployeeApiModel model) => new()
    {
        Id = model.Id,
        LastName = model.LastName ?? string.Empty,
        FirstName = model.FirstName ?? string.Empty,
        Title = model.Title ?? string.Empty,
        ReportsTo = model.ReportsTo,
        BirthDate = model.BirthDate,
        HireDate = model.HireDate,
        Address = model.Address ?? string.Empty,
        City = model.City ?? string.Empty,
        State = model.State ?? string.Empty,
        Country = model.Country ?? string.Empty,
        PostalCode = model.PostalCode ?? string.Empty,
        Phone = model.Phone ?? string.Empty,
        Fax = model.Fax ?? string.Empty,
        Email = model.Email ?? string.Empty
    };

    public static GenreApiModel ToApiModel(this Genre entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name
    };

    public static Genre ToEntity(this GenreApiModel model) => new()
    {
        Id = model.Id,
        Name = model.Name
    };

    public static MediaTypeApiModel ToApiModel(this MediaType entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name
    };

    public static MediaType ToEntity(this MediaTypeApiModel model) => new()
    {
        Id = model.Id,
        Name = model.Name
    };

    public static List<CustomerApiModel> ToApiModels(this IEnumerable<Customer> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<EmployeeApiModel> ToApiModels(this IEnumerable<Employee> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<GenreApiModel> ToApiModels(this IEnumerable<Genre> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<MediaTypeApiModel> ToApiModels(this IEnumerable<MediaType> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();
}
