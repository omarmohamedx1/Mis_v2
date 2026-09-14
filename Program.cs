using Npgsql;
var cs = args[0];
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
Console.WriteLine("OPEN_OK");
async Task Q(string sql) {
  await using var cmd = new NpgsqlCommand(sql, conn);
  await using var r = await cmd.ExecuteReaderAsync();
  var cols = Enumerable.Range(0, r.FieldCount).Select(r.GetName);
  Console.WriteLine(string.Join(" | ", cols));
  while (await r.ReadAsync()) {
    var vals = Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "NULL" : r.GetValue(i)?.ToString());
    Console.WriteLine(string.Join(" | ", vals));
  }
  Console.WriteLine("---");
}
Console.WriteLine("===TABLES===");
await Q(@"SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND (
 table_name ILIKE '%Excuse%' OR table_name ILIKE '%Mission%' OR table_name ILIKE '%Visit%' OR table_name ILIKE '%Attend%'
 OR table_name ILIKE '%Audit%' OR table_name ILIKE '%Attach%' OR table_name ILIKE '%Hr%' OR table_name IN ('Users','Employees')
 OR table_name ILIKE '%LeaveType%' OR table_name ILIKE '%DocumentType%' OR table_name ILIKE '%Permission%' OR table_name ILIKE '%Role%'
) ORDER BY 1");
Console.WriteLine("===HrExcuseMissions===");
await Q(@"SELECT column_name, data_type, is_nullable, column_default FROM information_schema.columns WHERE table_schema='public' AND table_name='HrExcuseMissions' ORDER BY ordinal_position");
Console.WriteLine("===HrExcuseAttachments===");
await Q(@"SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='HrExcuseAttachments' ORDER BY ordinal_position");
Console.WriteLine("===CONSTRAINTS===");
await Q(@"SELECT c.conname, pg_get_constraintdef(c.oid) FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace WHERE n.nspname='public' AND t.relname IN ('HrExcuseMissions','HrExcuseAttachments') ORDER BY 1");
Console.WriteLine("===INDEXES===");
await Q(@"SELECT tablename, indexname, indexdef FROM pg_indexes WHERE schemaname='public' AND tablename IN ('HrExcuseMissions','HrExcuseAttachments') ORDER BY 1,2");
Console.WriteLine("===MIGRATIONS===");
await Q(@"SELECT ""MigrationId"" FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" ILIKE '%Excuse%' OR ""MigrationId"" ILIKE '%Social%' OR ""MigrationId"" ILIKE '%Mobile%' ORDER BY 1");
Console.WriteLine("===Users.EmployeeId===");
await Q(@"SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='Users' AND column_name='EmployeeId'");
Console.WriteLine("===COUNTS===");
await Q(@"SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name='HrExcuseMissions') AS missions_exist, EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name='HrExcuseAttachments') AS attachments_exist");
