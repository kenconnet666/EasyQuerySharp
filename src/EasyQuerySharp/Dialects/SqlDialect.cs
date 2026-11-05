using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>写一个函数调用的委托：参数已翻译好，用 <paramref name="r"/> 输出。</summary>
public delegate void FunctionWriter(SqlRenderer r, IReadOnlyList<SqlExpr> args);

/// <summary>
/// 方言：决定标识符引用、占位符、分页、函数等写法。只负责渲染，不依赖任何驱动。
/// 子类只覆盖与基类不同的部分，例如兼容库继承被兼容的方言（PolarDB 用 MySQL 方言）。
/// 函数写法集中在 <see cref="RegisterFunctions"/>：基类登记标准 SQL 的写法，子类覆盖差异。
/// </summary>
public abstract class SqlDialect
{
    private Dictionary<string, FunctionWriter>? _functions;

    /// <summary>方言名，用于报错与能力判断，如 <c>mysql</c>、<c>pgsql</c>。</summary>
    public abstract string Name { get; }

    /// <summary>给标识符加引号。默认用双引号（标准 SQL）。</summary>
    public virtual string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>驱动执行时的第 <paramref name="index"/> 个参数占位符（从 0 开始）；默认 <c>@p0</c>。</summary>
    public virtual string ParameterPlaceholder(int index) => "@p" + index;

    /// <summary>驱动参数对象的名字，与 <see cref="ParameterPlaceholder"/> 对应。</summary>
    public virtual string ParameterName(int index) => "p" + index;

    /// <summary>
    /// 插入后取自增键的方式：返回 null 表示在 INSERT 后加 <c>RETURNING 列</c> 直接读出；
    /// 否则是同一连接上紧接着执行的查询（如 MySQL 的 <c>SELECT LAST_INSERT_ID()</c>）。
    /// </summary>
    public virtual string? LastInsertIdQuery => null;

    /// <summary>写分页子句（追加在 ORDER BY 之后）。默认 <c>LIMIT n OFFSET m</c>。</summary>
    public virtual void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Limit is { } limit) renderer.Append(" LIMIT ").Append(limit.ToString(CultureInfo.InvariantCulture));
        if (query.Offset is > 0) renderer.Append(" OFFSET ").Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 写一条带分页的查询：<paramref name="writeUnpaged"/> 输出不含分页的完整 SELECT。
    /// 默认在其后追加 <see cref="WritePaging"/>；需要包一层的方言（Oracle 的 ROWNUM、SQL Server 2008 的 ROW_NUMBER）覆盖它。
    /// </summary>
    public virtual void WritePagedSelect(SqlRenderer renderer, SelectQuery query, Action writeUnpaged)
    {
        writeUnpaged();
        WritePaging(renderer, query);
    }

    /// <summary>递归 CTE 的开头（树查询 AsTreeCte）：默认 <c>WITH RECURSIVE </c>；SQL Server、Oracle 不写 RECURSIVE。</summary>
    public virtual string RecursiveWith => "WITH RECURSIVE ";

    /// <summary>UPDATE 的 SET 列是否带表别名（<c>t.`a` = ?</c>）；PostgreSQL 不允许，覆盖成 false。</summary>
    public virtual bool QualifySetColumns => true;

    /// <summary>UPDATE / DELETE 目标表与别名之间的分隔；默认一个空格，SQLite 要求 <c> AS </c>。</summary>
    public virtual string WriteTargetAliasSeparator => " ";

    /// <summary>
    /// 写带连接的 UPDATE（SET 或条件里用到对一导航）。标准 SQL 没有统一写法，默认不支持；
    /// MySQL 写成 <c>UPDATE t INNER JOIN … SET …</c>，PostgreSQL 写成 <c>UPDATE t SET … FROM … WHERE</c>。
    /// </summary>
    public virtual void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update) =>
        throw new NotSupportedException($"{Name} 方言还不支持在 UPDATE 里使用对一导航（需要 JOIN），请改写成子查询");

    /// <summary>写带连接的 DELETE（条件里用到对一导航）。默认不支持；MySQL 写成 <c>DELETE t FROM … JOIN</c>，PostgreSQL 写成 <c>DELETE FROM … USING</c>。</summary>
    public virtual void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement) =>
        throw new NotSupportedException($"{Name} 方言还不支持在 DELETE 里使用对一导航（需要 JOIN），请改写成子查询");

    /// <summary>
    /// 写最外层查询末尾的行锁子句。默认是 MySQL 8、PostgreSQL、Oracle 共用的写法：
    /// <c> FOR UPDATE</c>、<c> FOR UPDATE NOWAIT</c>、<c> FOR UPDATE SKIP LOCKED</c>；不支持的方言覆盖成抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public virtual void WriteLock(SqlRenderer renderer, RowLock rowLock) => renderer.Append(rowLock switch
    {
        RowLock.ForUpdate => " FOR UPDATE",
        RowLock.ForUpdateNoWait => " FOR UPDATE NOWAIT",
        RowLock.ForUpdateSkipLocked => " FOR UPDATE SKIP LOCKED",
        _ => throw new ArgumentOutOfRangeException(nameof(rowLock), rowLock, null),
    });

    /// <summary>写 LIKE。默认 <c>x LIKE ?</c>（模式里已拼好 %）；方言可按 <see cref="LikeExpr.Mode"/> 改写。</summary>
    public virtual void WriteLike(SqlRenderer renderer, LikeExpr predicate) =>
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE " : " LIKE ").Write(predicate.Pattern);

    /// <summary>LIKE 值里的转义：反斜杠、%、_ 前加反斜杠（与 Java 各方言的 LikeFunctionTemplateGenerator 一致）。</summary>
    protected static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// 写 ORDER BY 的一项。指定了 NULL 位置时默认用 CASE 模拟（与 Java 的 OrderByNullsModeSQLFunction 一致，各库通用）：
    /// <c>CASE WHEN x IS NULL THEN 1 ELSE 0 END ASC,x ASC</c>；原生片段不追加方向。
    /// </summary>
    public virtual void WriteOrderByItem(SqlRenderer renderer, OrderByItem item)
    {
        if (item.Nulls is { } nulls)
            renderer.Append("CASE WHEN ").Write(item.Expr).Append(nulls == NullsOrder.Last ? " IS NULL THEN 1 ELSE 0 END ASC," : " IS NULL THEN 0 ELSE 1 END ASC,");
        renderer.Write(item.Expr);
        if (!item.Raw) renderer.Append(item.Descending ? " DESC" : " ASC");
    }

    /// <summary>
    /// 带组内排序的字符串聚合。默认是 PostgreSQL / SQLite 的写法：<c>STRING_AGG(x, ? ORDER BY …)</c>；
    /// MySQL 写成 <c>GROUP_CONCAT(x ORDER BY … SEPARATOR ?)</c>，SQL Server / Oracle 写成 <c>WITHIN GROUP (ORDER BY …)</c>。
    /// </summary>
    public virtual void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join)
    {
        renderer.Append(join.Distinct ? "STRING_AGG(DISTINCT " : "STRING_AGG(").Write(join.Value).Append(", ").Write(join.Separator).Append(" ");
        WriteOrderBy(renderer, join.Orders);
        renderer.Append(")");
    }

    /// <summary>写 <c>ORDER BY a ASC,b DESC</c>（项之间不加空格，与 Java 一致）。</summary>
    protected void WriteOrderBy(SqlRenderer renderer, IReadOnlyList<OrderByItem> orders)
    {
        renderer.Append("ORDER BY ");
        for (var i = 0; i < orders.Count; i++)
        {
            if (i > 0) renderer.Append(",");
            WriteOrderByItem(renderer, orders[i]);
        }
    }

    // ---------- 插入冲突（upsert）----------

    /// <summary>INSERT 的开头关键字；MySQL 忽略冲突时是 <c>INSERT IGNORE INTO </c>。</summary>
    public virtual string InsertKeyword(InsertStatement insert) => "INSERT INTO ";

    /// <summary>
    /// 写 VALUES 之后的冲突处理。默认是 PostgreSQL / SQLite 的写法（与 Java 的 PostgresSQLInsertSQLExpression 一致）：
    /// <c>ON CONFLICT (k) DO NOTHING</c>、<c>ON CONFLICT (k) DO UPDATE SET `c` = EXCLUDED.`c`</c>；没有要更新的列时不写。
    /// </summary>
    public virtual void WriteInsertConflict(SqlRenderer renderer, InsertConflict conflict)
    {
        if (conflict.Ignore)
        {
            renderer.Append(" ON CONFLICT");
            if (conflict.ConflictColumns.Count > 0) WriteConflictTarget(renderer, conflict);
            renderer.Append(" DO NOTHING");
            return;
        }
        if (conflict.UpdateColumns!.Count == 0) return;
        if (conflict.ConflictColumns.Count == 0) throw new InvalidOperationException("冲突更新需要约束列：实体没有非自增主键时请指定 conflictColumns");
        renderer.Append(" ON CONFLICT");
        WriteConflictTarget(renderer, conflict);
        renderer.Append(" DO UPDATE SET ");
        for (var i = 0; i < conflict.UpdateColumns.Count; i++)
        {
            var name = QuoteIdentifier(conflict.UpdateColumns[i].Name);
            renderer.Append(i == 0 ? "" : ", ").Append(name).Append(" = EXCLUDED.").Append(name);
        }
    }

    private void WriteConflictTarget(SqlRenderer renderer, InsertConflict conflict) =>
        renderer.Append(" (").Append(string.Join(",", conflict.ConflictColumns.Select(c => QuoteIdentifier(c.Name)))).Append(")");

    /// <summary>
    /// 类型转换的模板，<c>{0}</c> 是被转换的表达式（逐字对应 Java 各方言的 CastSQLFunction）。
    /// 默认是 MySQL 的写法；不支持的目标类型抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public virtual string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => " CAST({0} AS SIGNED)",
        _ when targetType == typeof(char) => "SUBSTR(CAST({0} AS CHAR), 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "CAST({0} AS TIME)",
        _ when targetType == typeof(DateOnly) => "CAST({0} AS DATE)",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "CAST({0} AS DATETIME)",
        _ when targetType == typeof(decimal) => "CAST({0} AS DECIMAL(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "CAST({0} AS DECIMAL(32,16))",
        _ when IsInteger(targetType) => "CAST({0} AS SIGNED)",
        _ when targetType == typeof(Guid) => "SUBSTR(CAST({0} AS CHAR), 1, 36)",
        _ when targetType == typeof(string) => "CAST({0} AS CHAR)",
        _ => throw new NotSupportedException($"{Name} 不支持转换到 {targetType.Name}"),
    };

    /// <summary>是否整数类型（byte / short / int / long 及无符号版本）。</summary>
    protected static bool IsInteger(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);

    // ---------- DDL（code-first）----------

    /// <summary>
    /// 建表语句。默认是标准写法：每列一行 <c>"列" 类型 NULL|NOT NULL</c>，最后是主键约束；MySQL 覆盖成 Java 版的格式。
    /// </summary>
    public virtual string CreateTableSql(Metadata.EntityMetadata entity)
    {
        var lines = entity.Columns.Select(ColumnDefinition).ToList();
        if (entity.KeyColumns.Count > 0 && !entity.KeyColumns.Any(InlinePrimaryKey))
            lines.Add($"PRIMARY KEY ({string.Join(",", entity.KeyColumns.Select(k => QuoteIdentifier(k.Name)))})");
        return CreateTableStatement(entity, string.Join(", ", lines));
    }

    /// <summary>包住列定义的建表语句；默认 <c>CREATE TABLE IF NOT EXISTS</c>，不支持 IF NOT EXISTS 的库（SQL Server）覆盖。</summary>
    protected virtual string CreateTableStatement(Metadata.EntityMetadata entity, string body) =>
        $"CREATE TABLE IF NOT EXISTS {QualifiedTable(entity)} ({body})";

    /// <summary>一列的定义：<c>"列" 类型 [自增] NULL|NOT NULL</c>。</summary>
    protected virtual string ColumnDefinition(Metadata.ColumnMetadata column) =>
        $"{QuoteIdentifier(column.Name)} {ColumnType(column)}{(column.IsGenerated ? " " + IdentityClause : "")} {(IsNullable(column) ? "NULL" : "NOT NULL")}";

    /// <summary>自增列的写法；默认是标准 SQL 的 <c>GENERATED BY DEFAULT AS IDENTITY</c>（PG、Oracle、Db2、H2）。</summary>
    protected virtual string IdentityClause => "GENERATED BY DEFAULT AS IDENTITY";

    /// <summary>主键是否已经写在列定义里（SQLite 的 <c>INTEGER PRIMARY KEY AUTOINCREMENT</c>），是的话不再单独写主键约束。</summary>
    protected virtual bool InlinePrimaryKey(Metadata.ColumnMetadata key) => false;

    /// <summary>删表语句（表不存在时不报错）。</summary>
    public virtual string DropTableIfExistsSql(Metadata.EntityMetadata entity) => $"DROP TABLE IF EXISTS {QualifiedTable(entity)}";

    /// <summary>列的数据库类型：<see cref="Metadata.ColumnAttribute.DbType"/> 优先，否则按 CLR 类型映射（<see cref="ClrColumnType"/>）。</summary>
    public string ColumnType(Metadata.ColumnMetadata column) =>
        column.DbType ?? ClrColumnType(Nullable.GetUnderlyingType(column.Converter?.ProviderType ?? column.ClrType) ?? column.Converter?.ProviderType ?? column.ClrType, column);

    /// <summary>CLR 类型到列类型的默认映射。默认用标准 SQL 类型名。</summary>
    protected virtual string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) => "VARCHAR(255)",
        _ when type == typeof(int) || type.IsEnum => "INTEGER",
        _ when type == typeof(long) => "BIGINT",
        _ when type == typeof(short) => "SMALLINT",
        _ when type == typeof(byte) => "SMALLINT",
        _ when type == typeof(bool) => "BOOLEAN",
        _ when type == typeof(decimal) => "DECIMAL(16,2)",
        _ when type == typeof(double) => "DOUBLE PRECISION",
        _ when type == typeof(float) => "REAL",
        _ when type == typeof(DateTime) => "TIMESTAMP",
        _ when type == typeof(DateTimeOffset) => "TIMESTAMP WITH TIME ZONE",
        _ when type == typeof(DateOnly) => "DATE",
        _ when type == typeof(TimeOnly) || type == typeof(TimeSpan) => "TIME",
        _ when type == typeof(Guid) => "CHAR(36)",
        _ when type == typeof(byte[]) => "BLOB",
        _ => throw new NotSupportedException($"{column} 的类型 {type.Name} 没有默认的列类型，请用 [Column(DbType = …)] 指定"),
    };

    /// <summary>列是否可空：主键与非可空值类型不可空，其余可空（与 Java 版一致）。</summary>
    protected static bool IsNullable(Metadata.ColumnMetadata column) =>
        !column.IsPrimaryKey && (!column.ClrType.IsValueType || Nullable.GetUnderlyingType(column.ClrType) is not null);

    /// <summary>带 schema 的表名。</summary>
    protected string QualifiedTable(Metadata.EntityMetadata entity) => QualifiedName(entity.Schema, entity.TableName);

    /// <summary>
    /// 带 schema 的表名：<c>`schema`.`table`</c>；schema 里的 <c>.</c> 分隔多级（如 SQL Server 的 <c>库.dbo</c>），各级分别加引号（与 Java 一致）。
    /// </summary>
    public string QualifiedName(string? schema, string table) =>
        schema is null ? QuoteIdentifier(table) : string.Join(".", schema.Split('.').Select(QuoteIdentifier)) + "." + QuoteIdentifier(table);

    /// <summary>写一个函数调用：查函数表，没有登记的按 <c>NAME(a,b)</c> 输出。</summary>
    public void WriteFunction(SqlRenderer renderer, FunctionExpr functionCall)
    {
        _functions ??= BuildFunctions();
        if (_functions.TryGetValue(functionCall.Name, out var writer)) writer(renderer, functionCall.Arguments);
        else Call(functionCall.Name)(renderer, functionCall.Arguments);
    }

    /// <summary>
    /// 登记函数写法。子类先调用 <c>base.RegisterFunctions(f)</c>，再覆盖与标准写法不同的部分。
    /// 名字见 <see cref="SqlFunctionNames"/>。
    /// </summary>
    protected virtual void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        f[SqlFunctionNames.NullOrDefault] = Call("COALESCE");
        f[SqlFunctionNames.EqualsWith] = (r, a) => r.Append("(CASE ").Write(a[0]).Append(" WHEN ").Write(a[1]).Append(" THEN 1 ELSE 0 END)");
        f[SqlFunctionNames.Concat] = Call("CONCAT");
        f[SqlFunctionNames.Length] = Call("CHAR_LENGTH");
        f[SqlFunctionNames.Trim] = Call("TRIM");
        f[SqlFunctionNames.SubString] = Call("SUBSTR");
        f[SqlFunctionNames.PadLeft] = Pad("LPAD", null);
        f[SqlFunctionNames.PadRight] = Pad("RPAD", null);
        f[SqlFunctionNames.CompareTo] = (r, a) => r.Append("(CASE WHEN ").Write(a[0]).Append(" = ").Write(a[1])
            .Append(" THEN 0 WHEN ").Write(a[0]).Append(" > ").Write(a[1]).Append(" THEN 1 ELSE -1 END)");
        f[SqlFunctionNames.IndexOf] = Call("INSTR");
        f[SqlFunctionNames.Ceiling] = Call("CEIL");
        f[SqlFunctionNames.Ln] = Call("LN");
        f[SqlFunctionNames.Truncate] = Call("TRUNC");
        f[SqlFunctionNames.CountDistinct] = Distinct("COUNT");
        f[SqlFunctionNames.SumDistinct] = Distinct("SUM");
        f[SqlFunctionNames.AvgDistinct] = Distinct("AVG");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG(").Write(a[0]).Append(", ").Write(a[1]).Append(")");
        f[SqlFunctionNames.DateFormat] = DateFormat("TO_CHAR({0},{1})", ("yyyy", "YYYY"), ("MM", "MM"), ("dd", "DD"), ("HH", "HH24"), ("mm", "MI"), ("ss", "SS"));
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("STRING_AGG(DISTINCT ").Write(a[0]).Append(", ").Write(a[1]).Append(")");
        f[SqlFunctionNames.Now] = Template("CURRENT_TIMESTAMP");
        f[SqlFunctionNames.Random] = Template("RANDOM()");
        f[SqlFunctionNames.UtcNow] = Template("(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");
        // 日期：标准写法取 PostgreSQL（与 Java 的 PgSQL 函数一致）
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "(({0})::timestamp+(({1})||' milliseconds')::interval)" : "(({0})::timestamp+(({1})||' " + Lower(unit) + "')::interval)",
            part => part switch
            {
                DatePart.DayOfYear => "extract(doy from ({0})::timestamp)",
                DatePart.DayOfWeek => "extract(dow from ({0})::timestamp)",
                DatePart.IsoDayOfWeek => "(CASE WHEN extract(dow from ({0})::timestamp) = 0 THEN 7 ELSE extract(dow from ({0})::timestamp) END)",
                _ => "extract(" + Lower(part) + " from ({0})::timestamp)",
            },
            unit => "(extract(epoch from ({1})::timestamp-({0})::timestamp)" + unit switch
            {
                DateUnit.Day => "/86400", DateUnit.Hour => "/3600", DateUnit.Minute => "/60", DateUnit.Second => "", _ => "*1000",
            } + ")::int");
    }

    /// <summary>
    /// 日期格式化：<paramref name="pattern"/> 写出带格式串的调用，格式串按 <paramref name="tokens"/>（C# 格式符 → 方言格式符，长的在前）转换后加引号。
    /// </summary>
    protected static FunctionWriter DateFormat(string pattern, params (string CSharp, string Sql)[] tokens) => (r, a) =>
    {
        var format = ((LiteralExpr)a[1]).Sql;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < format.Length;)
        {
            var (cs, sql) = tokens.FirstOrDefault(t => string.CompareOrdinal(format, i, t.CSharp, 0, t.CSharp.Length) == 0);
            if (cs is not null)
            {
                sb.Append(sql);
                i += cs.Length;
            }
            else sb.Append(format[i++]);
        }
        var parts = pattern.Split("{0}");
        var literal = "'" + sb.ToString().Replace("'", "''") + "'";
        r.Append(parts[0].Replace("{1}", literal));
        r.Write(a[0]);
        r.Append(parts[1].Replace("{1}", literal));
    };

    /// <summary>
    /// 登记日期加减、取部分、相差三组函数：<paramref name="add"/> 的模板里 {0} 是日期、{1} 是数量；
    /// <paramref name="diff"/> 的模板里 {0} 是起点、{1} 是终点。
    /// </summary>
    protected static void DateFunctions(IDictionary<string, FunctionWriter> f, Func<DateUnit, string> add, Func<DatePart, string> part, Func<DateUnit, string> diff)
    {
        foreach (var unit in Enum.GetValues<DateUnit>())
        {
            f[SqlFunctionNames.DateAdd(unit)] = Template(add(unit));
            f[SqlFunctionNames.DateDiff(unit)] = Template(diff(unit));
        }
        foreach (var p in Enum.GetValues<DatePart>()) f[SqlFunctionNames.DatePartOf(p)] = Template(part(p));
    }

    /// <summary>小写的单位 / 部分名，用于拼模板（<c>day</c>、<c>hour</c>）。</summary>
    protected static string Lower<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    /// <summary>按模板输出：<c>{n}</c> 处写第 n 个参数，其余原样。</summary>
    protected static FunctionWriter Template(string template) => (r, a) =>
    {
        var start = 0;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{' || i + 2 >= template.Length || !char.IsAsciiDigit(template[i + 1]) || template[i + 2] != '}') continue;
            r.Append(template[start..i]).Write(a[template[i + 1] - '0']);
            start = i + 3;
            i += 2;
        }
        r.Append(template[start..]);
    };

    /// <summary><c>NAME(a,b)</c>。</summary>
    protected static FunctionWriter Call(string name) => (r, a) => r.Append(name).Append("(").WriteList(a, ",").Append(")");

    /// <summary><c>NAME(DISTINCT x)</c>。</summary>
    protected static FunctionWriter Distinct(string name) => (r, a) => r.Append(name).Append("(DISTINCT ").Write(a[0]).Append(")");

    /// <summary>参数顺序与 C# 不同的调用：<paramref name="order"/> 给出输出时依次取第几个参数。</summary>
    protected static FunctionWriter Reordered(string name, params int[] order) =>
        (r, a) => r.Append(name).Append("(").WriteList(order.Where(i => i < a.Count).Select(i => a[i]).ToList(), ",").Append(")");

    /// <summary><c>LPAD(s, n)</c> / <c>LPAD(s, n, pad)</c>：Java 版在长度前留一个空格；<paramref name="defaultPad"/> 不为 null 时没给填充字符也写出它。</summary>
    protected static FunctionWriter Pad(string name, string? defaultPad) => (r, a) =>
    {
        r.Append(name).Append("(").Write(a[0]).Append(", ").Write(a[1]);
        if (a.Count > 2) r.Append(", ").Write(a[2]);
        else if (defaultPad is not null) r.Append(", ").Append(defaultPad);
        r.Append(")");
    };

    /// <summary>用分隔符连接参数（不加括号），如 Oracle 的 <c>a || b</c>。</summary>
    protected static FunctionWriter Infix(string separator) => (r, a) => r.WriteList(a, separator);

    private Dictionary<string, FunctionWriter> BuildFunctions()
    {
        var functions = new Dictionary<string, FunctionWriter>(StringComparer.Ordinal);
        RegisterFunctions(functions);
        return functions;
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
