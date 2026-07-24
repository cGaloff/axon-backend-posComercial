using MediatR;

namespace Axon.Application.Reports.Queries;

public record GetSalesByEmployeeReportQuery(DateTime FromDate, DateTime ToDate) : IRequest<SalesByEmployeeReportDto>;

public record SalesByEmployeeReportDto(List<EmployeeSalesDto> Employees);

public record EmployeeSalesDto(Guid UserId, string UserName, int TotalTransactions, decimal TotalRevenue);
