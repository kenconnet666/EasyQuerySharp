using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Querying.Includes;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 把投影 lambda 拆成两部分：SELECT 列表（交给 SQL），以及按列序号重建结果对象的读取函数（交给物化）。
/// <list type="bullet">
/// <item>投影到整个实体（<c>u => u</c>）：选出实体的全部默认列；</item>
/// <item>new 匿名类型 / record / 成员初始化：每个成员递归处理，标量成员一列；</item>
/// <item>标量：一列。</item>
/// </list>
/// 列别名规则与 Java 版的 DTO 映射一致：别名是成员名按命名约定转换后的列名，与源列同名时省略 AS。
/// </summary>
internal sealed class ProjectionCompiler(ExpressionTranslator translator, NameConversion naming, bool alwaysAlias = false, bool neverAlias = false)
{
    private readonly ParameterExpression _reader = Expression.Parameter(typeof(DbDataReader), "reader");
    private readonly List<SelectColumn> _columns = [];

    /// <summary>投影出的列。</summary>
    public IReadOnlyList<SelectColumn> Columns => _columns;

    /// <summary>成员名 → 输出列名。投影作为派生表时，外层按成员名找列。</summary>
    public Dictionary<string, string> MemberColumns { get; } = new(StringComparer.Ordinal);

    /// <summary>写了 AS 的成员。作为派生表再投影时，外层只给这些成员写 AS（与 Java 一致：内层带别名的列，外层也带）。</summary>
    public HashSet<string> AliasedMembers { get; } = new(StringComparer.Ordinal);

    /// <summary>投影的是整个实体时的元数据（派生表据此按实体物化）；否则为 null。</summary>
    public EntityMetadata? Entity { get; private set; }

    /// <summary>嵌在投影里的整个实体（<c>new { b, Rn = … }</c>）：成员名 → 元数据；它的列以 <c>b.Star</c> 登记在 <see cref="MemberColumns"/>。</summary>
    public Dictionary<string, EntityMetadata> NestedEntities { get; } = new(StringComparer.Ordinal);

    private Expression? _shaped;
    private readonly List<(IncludePlan Plan, EntityBinding Owner)> _includes = [];

    /// <summary>投影里的导航带出（主查询执行后由 <see cref="IncludeRunner"/> 依次执行）。</summary>
    public IReadOnlyList<IncludePlan> Includes => _includes.Select(i => i.Plan).ToList();

    /// <summary>调用方要求的关联键（作为别人的子查询时，用来把行挂回父对象）在结果里的列序号。</summary>
    public int[] RelationOrdinals { get; private set; } = [];

    /// <summary>
    /// 拆解投影：登记 SELECT 列，并构造（尚未编译的）读取表达式。
    /// 带出需要的父键列、调用方要求的关联键列追加在最后：已经原样选出的列直接复用，否则写成 <c>列 AS __relation__列名</c>（与 Java 版一致）。
    /// </summary>
    public void Prepare(Expression body, SourceBinding? root = null, IReadOnlyList<ColumnMetadata>? relationKeys = null, IReadOnlyList<IncludeSpec>? includes = null)
    {
        _shaped = Shape(body, member: null, topLevel: true);
        if (includes is { Count: > 0 })
            _shaped = WithIncludes(_shaped, root as EntityBinding ?? throw new NotSupportedException("Include 只能用在实体查询上"), includes);
        foreach (var (plan, owner) in _includes)
            plan.ParentKeyOrdinals = plan.Navigation.SelfColumns.Select(c => RelationColumn(owner.Table, c)).ToArray();
        if (relationKeys is not null && root is not null)
            RelationOrdinals = relationKeys.Select(c => RelationColumn(root.Table, c)).ToArray();
    }

    private int RelationColumn(TableSource table, ColumnMetadata column)
    {
        var alias = "__relation__" + column.Name;
        for (var i = 0; i < _columns.Count; i++)
        {
            var existing = _columns[i];
            if (existing.Expr is ColumnExpr ce && ce.Table == table && ce.Name == column.Name && (existing.Alias is null || existing.Alias == alias)) return i;
        }
        _columns.Add(new SelectColumn(new ColumnExpr(table, column.Name, column), alias));
        return _columns.Count - 1;
    }

    /// <summary>编译读取函数；须先调用 <see cref="Prepare"/>。</summary>
    public Func<DbDataReader, T> Compile<T>()
    {
        var shaped = _shaped ?? throw new InvalidOperationException("投影还没有 Prepare");
        return Expression.Lambda<Func<DbDataReader, T>>(Coerce(shaped, typeof(T)), _reader).Compile();
    }

    private Expression Shape(Expression body, MemberInfo? member, bool topLevel)
    {
        if (translator.BindingOf(body) is { } binding)
            return binding is DerivedBinding { Entity: null } dto ? ShapeDerivedDto(dto, body.Type, topLevel) : ShapeEntity(binding, body.Type, topLevel, member);

        switch (body)
        {
            case ListInitExpression li when IsMap(li.Type):
                return ShapeMap(li);
            // new Dictionary<string, object?>()：不指定列，SELECT *（Java 的 new MapProxy()）
            case NewExpression { Arguments.Count: 0 } n when topLevel && IsMap(n.Type):
                _columns.Add(new SelectColumn(new FragmentExpr("*", []), null));
                return Expression.Call(ReadMapMethod, _reader);
            case MethodCallExpression call when IsColumnsCall(call):
                return ShapeColumnsMap(call);
            // Sql.ClientSide(x, f)：SQL 里只有 x（列别名照常按成员），读出后调用 f
            case MethodCallExpression { Method.Name: nameof(Sql.ClientSide) } call when call.Method.DeclaringType == typeof(Sql):
            {
                // 换算可以是 lambda，也可以是方法组等任何能在客户端求值成委托的表达式
                Expression convert = ExpressionTranslator.StripConvert(call.Arguments[1]) is LambdaExpression lambda
                    ? lambda
                    : Expression.Constant(ClientEvaluator.Evaluate(call.Arguments[1]), call.Arguments[1].Type);
                var input = call.Method.GetGenericArguments()[0];
                return Expression.Invoke(convert, Coerce(Shape(call.Arguments[0], member, false), input));
            }
            case NewExpression n when n.Constructor is not null:
            {
                var members = TargetMembers(n);
                var toOne = new List<(IncludePlan Plan, MemberInfo Member)>();
                var args = n.Arguments.Select((a, i) => Coerce(ShapeMember(a, members[i], toOne), a.Type)).ToList();
                Expression created = n.Members is null ? Expression.New(n.Constructor, args) : Expression.New(n.Constructor, args, n.Members);
                return RegisterToOne(created, toOne);
            }
            case MemberInitExpression mi:
            {
                var toOne = new List<(IncludePlan Plan, MemberInfo Member)>();
                var newExpr = (NewExpression)Shape(mi.NewExpression, member, false);
                var bindings = mi.Bindings.Cast<MemberAssignment>()
                    .Select(b => Expression.Bind(b.Member, Coerce(ShapeMember(b.Expression, b.Member, toOne), b.Expression.Type))).ToList();
                return RegisterToOne(Expression.MemberInit(newExpr, bindings), toOne);
            }
            default:
                return ShapeScalar(body, member);
        }
    }

    /// <summary>
    /// 一个成员的值：集合导航（<c>u.Cards</c>、<c>u.Cards.Where(…).Select(…).ToList()</c>）是带出，先放一个空 List，主查询后填充；
    /// 整个对一导航对象（<c>c.Bank</c>）也是带出（单独的 IN 查询），构造完父对象后回填；其余照常翻译。
    /// </summary>
    private Expression ShapeMember(Expression value, MemberInfo? member, List<(IncludePlan Plan, MemberInfo Member)> toOne)
    {
        if (member is not null && TryInclude(value) is { } plan)
        {
            if (plan.IsCollection)
                return Expression.Call(Expression.Constant(plan), typeof(IncludePlan).GetMethod(nameof(IncludePlan.Register))!, _reader,
                    Expression.Constant(null, typeof(Action<object?>)));
            toOne.Add((plan, member));
            return Expression.Default(value.Type);
        }
        return Shape(value, member, false);
    }

    private IncludePlan? TryInclude(Expression value)
    {
        var inner = value;
        if (inner is MethodCallExpression { Method.Name: nameof(Enumerable.ToList) or nameof(Enumerable.ToArray) } materialize
            && materialize.Method.DeclaringType == typeof(Enumerable))
            inner = materialize.Arguments[0];

        LambdaExpression? selector = null;
        var filters = new List<LambdaExpression>();
        while (inner is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Method.Name is nameof(Enumerable.Select) or nameof(Enumerable.Where))
        {
            var lambda = (LambdaExpression)ExpressionTranslator.StripConvert(mc.Arguments[1]);
            if (mc.Method.Name == nameof(Enumerable.Select))
            {
                if (selector is not null || filters.Count > 0) return null;
                selector = lambda;
            }
            else filters.Insert(0, lambda);
            inner = mc.Arguments[0];
        }

        if (inner is not MemberExpression { Expression: { } ownerExpr } navMember || translator.BindingOf(ownerExpr) is not EntityBinding owner) return null;
        if (translator.FindNavigation(owner, navMember.Member.Name) is not { } nav) return null;
        if (!nav.IsCollection && (selector is not null || filters.Count > 0 || inner != value)) return null;

        var plan = new IncludePlan(nav, selector?.ReturnType ?? nav.Target.Type, selector, filters, nav.IsCollection);
        _includes.Add((plan, owner));
        return plan;
    }

    // 实体查询的 Include：实体读出后，集合导航先放一个空 List（主查询后填充），对一导航登记回填函数
    private Expression WithIncludes(Expression entity, EntityBinding owner, IReadOnlyList<IncludeSpec> includes)
    {
        var instance = Expression.Variable(entity.Type, "entity");
        var body = new List<Expression> { Expression.Assign(instance, entity) };
        foreach (var spec in includes)
        {
            var nav = translator.FindNavigation(owner, spec.Member) ?? throw new NotSupportedException($"{owner.Entity.Type.Name}.{spec.Member} 不是导航属性");
            var plan = new IncludePlan(nav, nav.Target.Type, null, [], nav.IsCollection) { Configure = spec.Configure };
            _includes.Add((plan, owner));
            var property = owner.Entity.Type.GetProperty(spec.Member)!;
            var register = typeof(IncludePlan).GetMethod(nameof(IncludePlan.Register))!;
            if (nav.IsCollection)
            {
                body.Add(Expression.Assign(Expression.Property(instance, property),
                    Expression.Convert(Expression.Call(Expression.Constant(plan), register, _reader, Expression.Constant(null, typeof(Action<object?>))), property.PropertyType)));
                continue;
            }
            var child = Expression.Parameter(typeof(object), "child");
            var assign = Expression.Lambda<Action<object?>>(Expression.Assign(Expression.Property(instance, property), Expression.Convert(child, property.PropertyType)), child);
            body.Add(Expression.Call(Expression.Constant(plan), register, _reader, assign));
        }
        body.Add(instance);
        return Expression.Block([instance], body);
    }

    // 对一带出：父对象构造完后登记回填函数（构造函数参数 / init 属性都能通过反射赋值）
    private Expression RegisterToOne(Expression created, List<(IncludePlan Plan, MemberInfo Member)> toOne)
    {
        if (toOne.Count == 0) return created;
        var instance = Expression.Variable(created.Type, "parent");
        var body = new List<Expression> { Expression.Assign(instance, created) };
        foreach (var (plan, member) in toOne)
        {
            var property = member as PropertyInfo ?? created.Type.GetProperty(member.Name)
                           ?? throw new NotSupportedException($"{created.Type.Name}.{member.Name} 不是可回填的属性");
            var value = Expression.Parameter(typeof(object), "child");
            var assign = Expression.Lambda<Action<object?>>(
                Expression.Call(Expression.Constant(property), typeof(PropertyInfo).GetMethod(nameof(PropertyInfo.SetValue), [typeof(object), typeof(object)])!,
                    Expression.Convert(instance, typeof(object)), value),
                value);
            body.Add(Expression.Call(Expression.Constant(plan), typeof(IncludePlan).GetMethod(nameof(IncludePlan.Register))!, _reader, assign));
        }
        body.Add(instance);
        return Expression.Block([instance], body);
    }

    // 整个实体：顶层时（u => u）记下实体元数据与成员列；嵌在 DTO 里时（new { u, … }）LEFT JOIN 没连上的行读成 null
    private Expression ShapeEntity(SourceBinding binding, Type type, bool topLevel, MemberInfo? member = null)
    {
        var start = _columns.Count;
        var (entity, columns) = binding switch
        {
            EntityBinding e => (e.Entity, (IReadOnlyList<ColumnMetadata>)e.Entity.Columns.Where(c => c.AutoSelect).ToList()),
            DerivedBinding { Entity: { } meta } d => (meta, meta.Columns.Where(c => d.MemberColumns.ContainsKey(c.PropertyName)).ToList()),
            _ => throw new NotSupportedException($"不能把 {type.Name} 作为整体投影"),
        };
        foreach (var c in columns)
        {
            // 计算列就地展开，别名是列名（Java 的 sqlConversion 写法）
            if (binding is EntityBinding { } owner && c.Computed is { } computed)
            {
                AddColumn(translator.TranslateComputed(computed, owner), c.Name, topLevel ? c.PropertyName : member is null ? null : member.Name + "." + c.PropertyName);
                continue;
            }
            var sql = binding is DerivedBinding db ? new ColumnExpr(db.Table, db.MemberColumns[c.PropertyName], c) : ((EntityBinding)binding).EntityTable.Column(c);
            AddColumn(sql, alias: null, topLevel ? c.PropertyName : member is null ? null : member.Name + "." + c.PropertyName);
        }
        if (topLevel) Entity = entity;
        else if (member is not null) NestedEntities[member.Name] = entity;

        return Expression.Convert(
            Expression.Call(EntityMaterializer.ReadMethod, _reader, Expression.Constant(start), Expression.Constant(entity),
                Expression.Constant(columns), Expression.Constant(!topLevel)),
            type);
    }

    /// <summary>
    /// 派生表里的 DTO 整体再投影（投影之后再排序 / 计数时外层的 SELECT）：每个成员一列，内层写了别名的成员外层也写
    /// （Java 版的写法 <c>t1.`id` AS `id`</c>）；按无参构造 + 属性赋值，或按参数名匹配的构造函数（record / 匿名类型）重建。
    /// </summary>
    private Expression ShapeDerivedDto(DerivedBinding binding, Type type, bool topLevel)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (memberName, column) in binding.MemberColumns)
            ordinals[memberName] = AddColumn(new ColumnExpr(binding.Table, column), column, topLevel ? memberName : null, forceAlias: binding.AliasedMembers.Contains(memberName));
        if (topLevel)
            foreach (var (name, nested) in binding.NestedEntities) NestedEntities[name] = nested;

        Expression Read(string memberName, Type valueType) =>
            binding.NestedEntities.TryGetValue(memberName, out var nested) ? ReadNested(memberName, nested, valueType)
            : Expression.Call(ValueReader.ReadMethod.MakeGenericMethod(valueType), _reader,
                Expression.Constant(ordinals[memberName]), Expression.Constant(null, typeof(IValueConverter)));

        // 嵌着的实体：它的列登记顺序与元数据一致且连续，从第一列开始整体物化
        Expression ReadNested(string memberName, EntityMetadata nested, Type valueType)
        {
            var columns = nested.Columns.Where(c => ordinals.ContainsKey(memberName + "." + c.PropertyName)).ToList();
            var start = ordinals[memberName + "." + columns[0].PropertyName];
            return Expression.Convert(
                Expression.Call(EntityMaterializer.ReadMethod, _reader, Expression.Constant(start), Expression.Constant(nested),
                    Expression.Constant((IReadOnlyList<ColumnMetadata>)columns), Expression.Constant(true)),
                valueType);
        }

        // Map：每个键按原样放回字典
        if (IsMap(type))
            return Expression.ListInit(Expression.New(type),
                binding.MemberColumns.Keys.Select(k => Expression.ElementInit(type.GetMethod("Add")!, Expression.Constant(k), Read(k, typeof(object)))));

        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor is not null)
        {
            var bindings = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && (ordinals.ContainsKey(p.Name) || binding.NestedEntities.ContainsKey(p.Name)))
                .Select(p => (MemberBinding)Expression.Bind(p, Read(p.Name, p.PropertyType)));
            return Expression.MemberInit(Expression.New(ctor), bindings);
        }
        var parameterized = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return Expression.New(parameterized, parameterized.GetParameters().Select(p => Read(p.Name!, p.ParameterType)));
    }

    private static readonly MethodInfo ReadMapMethod = typeof(EntityMaterializer).GetMethod(nameof(EntityMaterializer.ReadDictionary), [typeof(DbDataReader)])!;
    private static readonly MethodInfo ReadMapRangeMethod = typeof(EntityMaterializer).GetMethod(nameof(EntityMaterializer.ReadDictionary), [typeof(DbDataReader), typeof(int), typeof(int)])!;

    private static bool IsMap(Type type) => typeof(IDictionary<string, object?>).IsAssignableFrom(type);

    private static bool IsColumnsCall(MethodCallExpression call) =>
        call.Method.DeclaringType == typeof(Sql) && call.Method.Name is nameof(Sql.Columns) or nameof(Sql.ColumnsExcept);

    /// <summary>
    /// <c>Sql.Columns(b)</c> / <c>Sql.ColumnsExcept(b, b.X, …)</c>：实体的（其余）全部列，不写 AS，读成以列名为键的字典
    /// （Java 的 MapProxy.selectAll / selectIgnores）。
    /// </summary>
    private Expression ShapeColumnsMap(MethodCallExpression call)
    {
        if (translator.BindingOf(call.Arguments[0]) is not EntityBinding owner)
            throw new NotSupportedException($"Sql.Columns 只能用在实体上：{call.Arguments[0]}");
        var excluded = call.Arguments.Count > 1 && call.Arguments[1] is NewArrayExpression array
            ? array.Expressions.Select(e => ExpressionTranslator.StripConvert(e) is MemberExpression m ? m.Member.Name
                : throw new NotSupportedException($"Sql.ColumnsExcept 只能排除属性访问，如 b.CreateTime：{e}")).ToHashSet(StringComparer.Ordinal)
            : [];
        var start = _columns.Count;
        foreach (var c in owner.Entity.Columns.Where(c => c.AutoSelect && !excluded.Contains(c.PropertyName)))
            AddColumn(owner.EntityTable.Column(c), alias: null, memberName: null);
        return Expression.Call(ReadMapRangeMethod, _reader, Expression.Constant(start), Expression.Constant(_columns.Count - start));
    }

    /// <summary>
    /// Map 投影 <c>new Dictionary&lt;string, object?&gt; { { "id", o.Id }, … }</c>（Java 的 MapProxy.put）：每个键一列，
    /// 别名就是键且总写 AS（与 Java 一致），读出后按原样的 Add 调用放回字典。
    /// 构造参数可以是 <c>Sql.Columns(b)</c>，先放实体的全部列再追加这些键。
    /// </summary>
    private Expression ShapeMap(ListInitExpression map)
    {
        var created = map.NewExpression;
        if (created.Arguments is [MethodCallExpression columns] && IsColumnsCall(columns))
            created = Expression.New(created.Constructor!, Coerce(ShapeColumnsMap(columns), created.Arguments[0].Type));
        var initializers = map.Initializers.Select(init =>
        {
            if (init.Arguments.Count != 2 || ClientEvaluator.Evaluate(init.Arguments[0]) is not string key)
                throw new NotSupportedException($"Map 投影只支持 {{ \"键\", 值 }} 形式的初始化：{init}");
            var value = ExpressionTranslator.StripConvert(init.Arguments[1]);
            var sql = ExpressionTranslator.AsValue(translator.Translate(value));
            var ordinal = AddColumn(sql, key, key, forceAlias: true);
            var read = Expression.Call(ValueReader.ReadMethod.MakeGenericMethod(value.Type), _reader, Expression.Constant(ordinal),
                Expression.Constant((sql as ColumnExpr)?.Column?.Converter, typeof(IValueConverter)));
            return Expression.ElementInit(init.AddMethod, Expression.Constant(key), Coerce(read, init.Arguments[1].Type));
        });
        return Expression.ListInit(created, initializers);
    }

    private Expression ShapeScalar(Expression body, MemberInfo? member)
    {
        var sql = ExpressionTranslator.AsValue(translator.Translate(body));
        var converter = (sql as ColumnExpr)?.Column?.Converter;
        // 分组投影里的组内列（g.Column(..)）按普通列处理：同名不写 AS（Java 的 groupTable() 列）
        var groupColumn = ExpressionTranslator.StripConvert(body) is MethodCallExpression { Method.Name: nameof(IGroup<object, object>.Column) } call
                          && ExpressionTranslator.IsGroupType(call.Method.DeclaringType!);
        var ordinal = AddColumn(sql, member is null ? null : ColumnName(member), member?.Name, plainColumn: groupColumn);
        return Expression.Call(ValueReader.ReadMethod.MakeGenericMethod(body.Type), _reader, Expression.Constant(ordinal),
            Expression.Constant(converter, typeof(IValueConverter)));
    }

    // 返回列序号；源列与别名同名时不写 AS（分组投影总写 AS；DTO 再投影沿用内层是否写了 AS，与 Java 一致）
    private int AddColumn(SqlExpr sql, string? alias, string? memberName, bool forceAlias = false, bool plainColumn = false)
    {
        var columnName = (sql as ColumnExpr)?.Name;
        if (neverAlias || (alias == columnName && !forceAlias && (!alwaysAlias || plainColumn))) alias = null;
        _columns.Add(new SelectColumn(sql, alias));
        if (memberName is not null && (alias ?? columnName) is { } output) MemberColumns.TryAdd(memberName, output);
        if (memberName is not null && alias is not null) AliasedMembers.Add(memberName);
        return _columns.Count - 1;
    }

    // new 表达式各参数对应的目标成员：匿名类型直接给出；record 主构造函数按参数名找同名属性（忽略大小写）
    private static MemberInfo?[] TargetMembers(NewExpression n)
    {
        if (n.Members is not null) return [.. n.Members];
        return n.Constructor!.GetParameters()
            .Select(p => (MemberInfo?)n.Type.GetProperty(p.Name ?? "", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase))
            .ToArray();
    }

    // 目标成员对应的列名：成员上 [Column("…")] 优先，否则按命名约定（Java 版 DTO 的映射规则）
    private string ColumnName(MemberInfo member) =>
        member.GetCustomAttribute<ColumnAttribute>()?.Name ?? naming.Convert(member.Name);

    private static Expression Coerce(Expression e, Type type) => e.Type == type ? e : Expression.Convert(e, type);
}
