namespace Api.Models;

/// <summary>
/// Роль: дефолтная (is_default=true, company_id=null — одна на все компании)
/// или кастомная (company_id обязателен — видна только своей компании).
/// company_id — компании из company-service (без FK, разные БД).
/// </summary>
public class Role
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required bool IsDefault { get; set; }
    public Guid? CompanyId { get; set; }
    public required DateTime CreatedAt { get; set; }
}
