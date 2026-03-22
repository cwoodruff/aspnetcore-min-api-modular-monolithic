using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class EmployeeRepository(AppDbContext context) : BaseRepository<Employee>(context), IEmployeeRepository
{
    public async Task<Employee> GetReportsTo(int id)
    {
        return (await _context.Employees.FindAsync(id))!;
    }

    public async Task<List<Employee>> GetDirectReports(int id)
    {
        return await _context.Employees.Where(e => e.ReportsTo == id).AsNoTracking().ToListAsync();
    }

    public async Task<EmployeeApiModel?> GetById(int id)
    {
        return await _context.Employees
            .Where(e => e.Id == id)
            .Select(e => new EmployeeApiModel
            {
                Id = e.Id,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Title = e.Title,
                ReportsTo = e.ReportsTo,
                BirthDate = e.BirthDate,
                HireDate = e.HireDate,
                Address = e.Address,
                City = e.City,
                State = e.State,
                Country = e.Country,
                PostalCode = e.PostalCode,
                Phone = e.Phone,
                Fax = e.Fax,
                Email = e.Email,
                ReportsToNavigation = e.ReportsToNavigation != null
                    ? (e.ReportsToNavigation.FirstName + " " + e.ReportsToNavigation.LastName)
                    : null,
                Customers = new List<CustomerApiModel>(), // avoid deep cycles
                InverseReportsToNavigation = new List<EmployeeApiModel>()
            })
            .AsNoTracking()
            .SingleOrDefaultAsync();
    }

    public async Task<Employee> GetToReports(int id)
    {
        return (await _context.Employees
            .FindAsync(_context.Employees.Where(e => e.Id == id)
                .Select(p => new { p.ReportsTo })
                .First()))!;
    }
}
