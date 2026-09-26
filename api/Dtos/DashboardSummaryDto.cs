namespace CarInsurance.Api.Dtos;

public sealed record DashboardSummaryDto(
    int CustomerTotal,
    int ActivePolicyTotal,
    int PendingUnderwritingTotal,
    int PendingClaimTotal);
