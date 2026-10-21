namespace Api.Contracts;

/// <summary>Компания из internal- ответа company-service ({ id, name, bankProfileId }).</summary>
public class CompanyInfoResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? BankProfileId { get; set; }
}
