namespace Api.Contracts;

/// <summary>
/// Компания из internal-ответа company-service ({ id, name, creatorId }).
/// CreatorId нужен user-service для проверок «менять роли может только создатель компании».
/// bankProfileId здесь НЕ маппим (там строковый код профиля, а не GUID — десериализация падала).
/// </summary>
public class CompanyInfoResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
}
