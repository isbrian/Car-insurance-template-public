using System.Globalization;
using System.Text;

namespace CarInsurance.Api.Services;

public sealed class PolicyPdfGenerator : IPolicyPdfGenerator
{
    public byte[] Generate(PolicyPdfData data)
    {
        var lines = new[]
        {
            "Motor Insurance Policy",
            $"Policy Number: {data.PolicyNumber}",
            $"Template Version: {data.TemplateVersion}",
            $"Issued At (UTC): {data.IssuedAtUtc:yyyy-MM-dd HH:mm:ss}",
            $"Application: {data.ApplicationNumber}",
            $"Quote: {data.QuoteNumber}",
            $"Payment: {data.PaymentNumber}",
            $"Customer: {ToPdfAscii(data.CustomerName)}",
            $"Insured: {ToPdfAscii(data.InsuredName)}",
            $"License Plate: {ToPdfAscii(data.LicensePlate)}",
            $"VIN: {ToPdfAscii(data.Vin)}",
            $"Vehicle Model: {ToPdfAscii(data.MakeModelCode)}",
            $"Usage: {ToPdfAscii(data.UsageType)}",
            $"Insurance Period: {data.InsuranceStartDate:yyyy-MM-dd} - {data.InsuranceEndDate:yyyy-MM-dd}",
            $"Total Premium: {data.TotalPremium.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"Payment Frequency: {data.PaymentFrequency}",
            $"Payment Method: {data.PaymentMethod}",
            $"Sales Agent: {ToPdfAscii(data.SalesAgent)}",
            $"Channel: {ToPdfAscii(data.Channel)}",
            $"Rate Version: {ToPdfAscii(data.RateVersion)}"
        };

        var content = BuildContentStream(lines);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Length + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            WriteAscii(stream, $"{offsets[index]:0000000000} 00000 n \n");
        }

        WriteAscii(
            stream,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return stream.ToArray();
    }

    private static string BuildContentStream(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        builder.Append("BT\n/F1 12 Tf\n50 790 Td\n");
        var first = true;
        foreach (var line in lines)
        {
            if (!first)
            {
                builder.Append("0 -18 Td\n");
            }

            builder.Append('(')
                .Append(EscapePdfString(ToPdfAscii(line)))
                .Append(") Tj\n");
            first = false;
        }

        builder.Append("ET");
        return builder.ToString();
    }

    private static string ToPdfAscii(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(character is >= ' ' and <= '~' ? character : '?');
        }

        return builder.ToString();
    }

    private static string EscapePdfString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
