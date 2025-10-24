using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Entities;

public partial class Employee
{
    public int Id { get; set; }

    public string? LastName { get; set; }

    public string? FirstName { get; set; }

    public string? Title { get; set; }

    public int? ReportsTo { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual ICollection<Employee> InverseReportsToNavigation { get; set; } = new List<Employee>();

    public virtual Employee? ReportsToNavigation { get; set; }

    public EmployeeApiModel Convert() =>
        new()
        {
            Id = Id,
            LastName = LastName,
            FirstName = FirstName,
            Title = Title,
            ReportsTo = ReportsTo,
            BirthDate = BirthDate,
            HireDate = HireDate,
            Address = Address,
            City = City,
            State = State,
            Country = Country,
            PostalCode = PostalCode,
            Phone = Phone,
            Fax = Fax,
            Email = Email,
            ReportsToNavigation = ReportsToNavigation != null ? $"{ReportsToNavigation.FirstName} {ReportsToNavigation.LastName}" : null,
            InverseReportsToNavigation = InverseReportsToNavigation.Select(e => e.Convert()).ToList(),
            Customers = Customers.Select(c => c.Convert()).ToList()
        };
}
