namespace SharedKernel.Persistence.Entities;

public partial class Employee
{
    public int Id { get; set; }

    public string? LastName { get; set; }

    public string? FirstName { get; set; }

    public string? Title { get; set; }

    public int? ReportsTo { get; set; }

    public string? BirthDate { get; set; }

    public string? HireDate { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<Employee> InverseReportsToNavigation { get; set; } = new List<Employee>();

    public virtual Employee? ReportsToNavigation { get; set; }
}
