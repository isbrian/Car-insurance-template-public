using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class CustomerService(
    ICustomerRepository customerRepository,
    IAuditLogService auditLogService) : ICustomerService
{
    public async Task<CustomerListResponse> GetAllAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await customerRepository.SearchAsync(
            keyword,
            page,
            pageSize,
            cancellationToken);

        return new CustomerListResponse(
            items.Select(ToDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<CustomerDetails?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(id, cancellationToken);
        return customer is null ? null : ToDetails(customer);
    }

    public async Task<CustomerPiiUnmaskResponseDto?> UnmaskPiiAsync(
        int id,
        CustomerPiiUnmaskRequestDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            throw new CustomerPiiUnmaskValidationException("reason 不可為空白。");
        }
        if (reason.Length > 500)
        {
            throw new CustomerPiiUnmaskValidationException("reason 不可超過 500 個字元。");
        }

        var customer = await customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var actor = RequireAuditActor(auditActor);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "customer.pii.unmasked",
                "Customer",
                customer.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    fields = new[] { "nationalId", "phone" },
                    reason
                }),
                actor.SourceIp),
            cancellationToken);

        return new CustomerPiiUnmaskResponseDto(
            customer.Id,
            customer.NationalId,
            customer.Phone);
    }

    public async Task<CustomerDto> CreateAsync(
        CustomerCreateDto request,
        CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Name = request.Name.Trim(),
            NationalId = request.NationalId.Trim().ToUpperInvariant(),
            Phone = request.Phone.Trim(),
            Email = request.Email,
            BirthDate = request.BirthDate,
            Address = request.Address,
            CreatedAtUtc = DateTime.UtcNow
        };

        await customerRepository.AddAsync(customer, cancellationToken);
        await customerRepository.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task<CustomerDto?> UpdateAsync(
        int id,
        CustomerCreateDto request,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        customer.Name = request.Name.Trim();
        customer.NationalId = request.NationalId.Trim().ToUpperInvariant();
        customer.Phone = request.Phone.Trim();
        customer.Email = request.Email;
        customer.BirthDate = request.BirthDate;
        customer.Address = request.Address;

        await customerRepository.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public Task<bool> ExistsByNationalIdAsync(
        string nationalId,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        return customerRepository.ExistsByNationalIdAsync(
            nationalId.Trim().ToUpperInvariant(),
            excludingId,
            cancellationToken);
    }

    private static CustomerDetails ToDetails(Customer customer)
    {
        return new CustomerDetails(
            customer.Id,
            customer.Name,
            MaskNationalId(customer.NationalId),
            MaskPhone(customer.Phone),
            customer.Email,
            customer.BirthDate,
            customer.Address,
            customer.IsVerified,
            customer.ClaimRecordLevel,
            customer.CreatedAtUtc,
            customer.Vehicles.Select(vehicle => new VehicleSummary(
                vehicle.Id,
                vehicle.LicensePlate,
                vehicle.Vin,
                vehicle.MakeModelCode,
                vehicle.ManufactureDate?.Year ?? 0,
                vehicle.UsageType)).ToList());
    }

    private static CustomerDto ToDto(Customer customer)
    {
        return new CustomerDto(
            customer.Id,
            customer.Name,
            MaskNationalId(customer.NationalId),
            MaskPhone(customer.Phone),
            customer.Email,
            customer.BirthDate,
            customer.Address,
            customer.IsVerified,
            customer.ClaimRecordLevel,
            customer.CreatedAtUtc);
    }

    private static AuditActorContext RequireAuditActor(AuditActorContext auditActor)
    {
        var actor = auditActor.Actor.Trim();
        var role = auditActor.Role.Trim();
        var sourceIp = auditActor.SourceIp.Trim();
        if (actor.Length == 0 || role.Length == 0 || sourceIp.Length == 0)
        {
            throw new InvalidOperationException("缺少可信的稽核操作人、角色或來源 IP。");
        }

        return new AuditActorContext(actor, role, sourceIp);
    }

    private static string MaskNationalId(string value)
    {
        return value.Length <= 3 ? "***" : $"{value[0]}****{value[^2..]}";
    }

    private static string MaskPhone(string value)
    {
        return value.Length <= 5 ? "*****" : $"{value[..2]}**-***-{value[^3..]}";
    }
}

public sealed class CustomerPiiUnmaskValidationException(string message) : Exception(message);
