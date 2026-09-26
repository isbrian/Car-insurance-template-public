using System.Text.Json;
using CarInsurance.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(CarInsuranceDbContext dbContext, IConfiguration configuration)
    {
        if (!await dbContext.Customers.AnyAsync())
        {
            var now = DateTime.UtcNow;
            await dbContext.Customers.AddRangeAsync(
                new Customer { Name = "王小明", NationalId = "A123456789", Phone = "0912345678", Email = "ming@example.com", BirthDate = new DateTime(1988, 5, 20, 0, 0, 0, DateTimeKind.Utc), Address = "台北市中正區重慶南路一段 1 號", IsVerified = true, CreatedAtUtc = now },
                new Customer { Name = "陳美玲", NationalId = "B223456789", Phone = "0922345678", Email = "mei@example.com", BirthDate = new DateTime(1992, 8, 12, 0, 0, 0, DateTimeKind.Utc), Address = "新北市板橋區文化路一段 2 號", IsVerified = true, CreatedAtUtc = now },
                new Customer { Name = "林志宏", NationalId = "C323456789", Phone = "0933345678", Email = "chihung@example.com", BirthDate = new DateTime(1985, 11, 3, 0, 0, 0, DateTimeKind.Utc), Address = "桃園市桃園區中正路 3 號", IsVerified = false, CreatedAtUtc = now });
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.Vehicles.AnyAsync())
        {
            var customers = await dbContext.Customers.OrderBy(x => x.Id).Take(2).ToListAsync();
            if (customers.Count >= 2)
            {
                await dbContext.Vehicles.AddRangeAsync(
                    new Vehicle { LicensePlate = "ABC-1234", Vin = "DEMO-VIN-000000000001", MakeModelCode = "TOYOTA-COROLLA", ManufactureDate = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc), UsageType = "自用", CustomerId = customers[0].Id },
                    new Vehicle { LicensePlate = "DEF-5678", Vin = "DEMO-VIN-000000000002", MakeModelCode = "HONDA-CRV", ManufactureDate = new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc), UsageType = "自用", CustomerId = customers[1].Id });
                await dbContext.SaveChangesAsync();
            }
        }

        var insuranceTypes = await dbContext.InsuranceTypes.ToDictionaryAsync(x => x.Code);
        var typeNow = DateTime.UtcNow;
        if (!insuranceTypes.ContainsKey("CTP"))
        {
            var entity = new InsuranceType { Code = "CTP", Name = "強制汽車責任保險", Description = "法定強制汽車責任保險", IsActive = true, CreatedAtUtc = typeNow };
            await dbContext.InsuranceTypes.AddAsync(entity);
            insuranceTypes[entity.Code] = entity;
        }
        if (!insuranceTypes.ContainsKey("TPL"))
        {
            var entity = new InsuranceType { Code = "TPL", Name = "第三人責任險", Description = "任意第三人責任保險", IsActive = true, CreatedAtUtc = typeNow };
            await dbContext.InsuranceTypes.AddAsync(entity);
            insuranceTypes[entity.Code] = entity;
        }
        if (!insuranceTypes.ContainsKey("BODY"))
        {
            var entity = new InsuranceType { Code = "BODY", Name = "車體損失險", Description = "車體損失保險", IsActive = true, CreatedAtUtc = typeNow };
            await dbContext.InsuranceTypes.AddAsync(entity);
            insuranceTypes[entity.Code] = entity;
        }
        await dbContext.SaveChangesAsync();

        var productsByCode = await dbContext.InsuranceProducts.ToDictionaryAsync(x => x.Code);
        var productNow = DateTime.UtcNow;
        if (!productsByCode.ContainsKey("CTP-AUTO"))
        {
            var entity = new InsuranceProduct { InsuranceTypeId = insuranceTypes["CTP"].Id, Code = "CTP-AUTO", Name = "汽車強制險", Description = "汽車強制責任保險 Demo 商品", IsActive = true, CreatedAtUtc = productNow };
            await dbContext.InsuranceProducts.AddAsync(entity);
            productsByCode[entity.Code] = entity;
        }
        if (!productsByCode.ContainsKey("TPL-200-50"))
        {
            var entity = new InsuranceProduct { InsuranceTypeId = insuranceTypes["TPL"].Id, Code = "TPL-200-50", Name = "第三人責任險 200萬/50萬", Description = "Demo 第三人責任險方案", IsActive = true, CreatedAtUtc = productNow };
            await dbContext.InsuranceProducts.AddAsync(entity);
            productsByCode[entity.Code] = entity;
        }
        if (!productsByCode.ContainsKey("BODY-B"))
        {
            var entity = new InsuranceProduct { InsuranceTypeId = insuranceTypes["BODY"].Id, Code = "BODY-B", Name = "車體損失險乙式", Description = "Demo 車體險乙式商品", IsActive = true, CreatedAtUtc = productNow };
            await dbContext.InsuranceProducts.AddAsync(entity);
            productsByCode[entity.Code] = entity;
        }
        await dbContext.SaveChangesAsync();

        var seedProductCodes = new[] { "CTP-AUTO", "TPL-200-50", "BODY-B" };
        var seedProducts = await dbContext.InsuranceProducts
            .Where(x => seedProductCodes.Contains(x.Code))
            .ToListAsync();
        var existingRateKeys = await dbContext.RateVersions
            .Where(x => x.Version == "provisional-v0.1")
            .Select(x => x.InsuranceProductId)
            .ToHashSetAsync();
        var rateNow = DateTime.UtcNow;
        foreach (var product in seedProducts.Where(x => !existingRateKeys.Contains(x.Id)))
        {
            await dbContext.RateVersions.AddAsync(new RateVersion
            {
                InsuranceProductId = product.Id,
                Version = "provisional-v0.1",
                EffectiveFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                Confidence = "provisional",
                IsActive = true,
                CreatedAtUtc = rateNow
            });
        }
        await dbContext.SaveChangesAsync();

        var ratesPath = Path.Combine(AppContext.BaseDirectory, "Data", "rates", "rates.provisional.json");
        var ratesJson = await File.ReadAllTextAsync(ratesPath);
        using var ratesDocument = JsonDocument.Parse(ratesJson);
        var meta = ratesDocument.RootElement.GetProperty("meta");
        var sourceVersion = meta.GetProperty("version").GetString()
            ?? throw new InvalidDataException("rates.provisional.json meta.version 不可為空。");
        var isProvisional = meta.GetProperty("isProvisional").GetBoolean();
        var displayWarning = meta.GetProperty("displayWarning").GetString()
            ?? throw new InvalidDataException("rates.provisional.json meta.displayWarning 不可為空。");

        var targetRateVersions = await dbContext.RateVersions
            .Include(x => x.InsuranceProduct)
            .Where(x => seedProductCodes.Contains(x.InsuranceProduct.Code) && x.Version == sourceVersion)
            .ToListAsync();
        var targetRateVersionIds = targetRateVersions.Select(x => x.Id).ToArray();
        var existingRateTables = await dbContext.RateTables
            .Where(x => targetRateVersionIds.Contains(x.RateVersionId))
            .ToDictionaryAsync(x => x.RateVersionId);
        var importedAt = DateTime.UtcNow;

        foreach (var rateVersion in targetRateVersions)
        {
            if (existingRateTables.TryGetValue(rateVersion.Id, out var existingRateTable))
            {
                if (existingRateTable.SourceVersion == sourceVersion && existingRateTable.JsonData == ratesJson)
                {
                    continue;
                }

                existingRateTable.SourceVersion = sourceVersion;
                existingRateTable.IsProvisional = isProvisional;
                existingRateTable.DisplayWarning = displayWarning;
                existingRateTable.JsonData = ratesJson;
                existingRateTable.ImportedAtUtc = importedAt;
                continue;
            }

            await dbContext.RateTables.AddAsync(new RateTable
            {
                RateVersionId = rateVersion.Id,
                SourceVersion = sourceVersion,
                IsProvisional = isProvisional,
                DisplayWarning = displayWarning,
                JsonData = ratesJson,
                ImportedAtUtc = importedAt
            });
        }

        await dbContext.SaveChangesAsync();

        var existingUnderwritingRuleCodes = await dbContext.UnderwritingRules
            .Select(x => x.Code)
            .ToHashSetAsync();
        var underwritingRuleNow = DateTime.UtcNow;
        var underwritingRules = new[]
        {
            new UnderwritingRule
            {
                Code = "UW-014",
                Name = "車齡超過 15 年",
                ConditionType = "vehicle_age_years_gt",
                ConditionJson = "{\"years\":15}",
                Action = "reject",
                Message = "車齡超過 15 年，符合拒保條件。",
                IsEnabled = true,
                CreatedAtUtc = underwritingRuleNow,
                UpdatedAtUtc = underwritingRuleNow
            },
            new UnderwritingRule
            {
                Code = "UW-032",
                Name = "近 3 年有理賠紀錄",
                ConditionType = "claim_count_last_3_years_gt",
                ConditionJson = "{\"count\":0}",
                Action = "manual_review",
                Message = "近 3 年有理賠紀錄，需人工覆核。",
                IsEnabled = true,
                CreatedAtUtc = underwritingRuleNow,
                UpdatedAtUtc = underwritingRuleNow
            },
            new UnderwritingRule
            {
                Code = "UW-051",
                Name = "行照未上傳",
                ConditionType = "document_missing",
                ConditionJson = "{\"documentType\":\"vehicle_registration\"}",
                Action = "block_issue",
                Message = "行照未上傳或補件未完成，阻擋出單。",
                IsEnabled = true,
                CreatedAtUtc = underwritingRuleNow,
                UpdatedAtUtc = underwritingRuleNow
            }
        };

        foreach (var rule in underwritingRules.Where(x => !existingUnderwritingRuleCodes.Contains(x.Code)))
        {
            await dbContext.UnderwritingRules.AddAsync(rule);
        }

        await dbContext.SaveChangesAsync();

        if (!await dbContext.UserAccounts.AnyAsync())
        {
            var demoPassword = configuration["Auth:DemoPassword"]
                ?? throw new InvalidOperationException("缺少必要設定 Auth:DemoPassword。");
            var now = DateTime.UtcNow;
            var users = new[]
            {
                new UserAccount { Username = "sales", Email = "sales@example.com", DisplayName = "王業務", Phone = "0912345128", Role = "業務員", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new UserAccount { Username = "underwriter", Email = "underwriter@example.com", DisplayName = "陳核保", Phone = "0922345128", Role = "核保員", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new UserAccount { Username = "claims", Email = "claims@example.com", DisplayName = "林理賠", Phone = "0933345128", Role = "理賠員", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new UserAccount { Username = "manager", Email = "manager@example.com", DisplayName = "李主管", Phone = "0944345128", Role = "主管", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new UserAccount { Username = "admin", Email = "admin@example.com", DisplayName = "系統管理員", Phone = "0955345128", Role = "系統管理員", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now }
            };

            var passwordHasher = new PasswordHasher<UserAccount>();
            foreach (var user in users)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, demoPassword);
            }

            await dbContext.UserAccounts.AddRangeAsync(users);
            await dbContext.SaveChangesAsync();
        }
    }
}
