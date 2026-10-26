using Api.Resources;

string[] queries = [UserSql.GetAll, UserSql.GetById, UserSql.GetByEmail, UserSql.ExistsByEmail, UserSql.Add,
    ViewerContextSql.Find, ViewerContextSql.Latest, ViewerContextSql.LockUser, ViewerContextSql.LastTime,
    ViewerContextSql.Insert, ViewerContextSql.UpdateUser];
if (queries.Any(string.IsNullOrWhiteSpace))
    throw new InvalidOperationException("Every UserSql query must resolve from the Api assembly resources.");
Console.WriteLine($"All {queries.Length} SQL resources loaded successfully.");
await ViewerContextChecks.Run();
