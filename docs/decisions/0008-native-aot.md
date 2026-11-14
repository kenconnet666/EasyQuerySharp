# 0008 裁剪与 Native AOT

状态：已定（2026-11-14）。

## 背景

1.0 不支持裁剪与 Native AOT，`EasyDb` 等入口标了 `RequiresUnreferencedCode` / `RequiresDynamicCode`。用户需要容器冷启动快、单文件小的部署方式，于是在不改公开 API 写法的前提下补上。

阻碍在三处：

1. **元数据靠反射**：实体、DTO、值对象的属性、特性、构造函数都在运行时读。使用者的代码往往只读属性、从不写，裁剪器看不到 setter 和构造函数的用法，就会删掉它们。
2. **运行时按值类型实例化泛型**：`ValueReader.Read<int>`、`List<int>`、`EnumNameConverter<Level>` 一类实例化，编译期看不出来，Native AOT 没有生成它们的代码。
3. **运行时编译表达式树**：读取函数、属性读写委托。Native AOT 下 `Compile()` 退回解释执行，能跑，但是慢。

## 决定

照 System.Text.Json 的思路，先让 Native AOT 下能用、结果与 JIT 一致；读取函数在 AOT 下解释执行的速度问题留到以后（见“不做”）。

### 1. 生成器保留使用者的类型

`EntityRootGenerator` 随核心包分发（与 `[Projection]` 生成器同一个程序集），在每个引用了核心包的项目里运行，不需要新的公开 API：

- **找类型**：EasyQuerySharp 的泛型方法与泛型类型的类型实参（`db.Query<User>()`、`Select<UserDto>()`、推断出来的 `db.Insert(user)`、`[MapFrom<User>]`）、标了 `[Table]` 的类，再顺着属性类型（导航、值对象、集合元素）与 `[Navigate(Mapping = …)]` 一路找全。
- **保留成员**：生成一个模块初始化方法，用 `[DynamicDependency(All, typeof(T))]` 保留这些类型的全部成员。
- **引用实例化**：属性是值类型集合时引用一次 `new List<元素>()`，属性是枚举时引用一次 `new EnumNameConverter<枚举>()`。这些代码放在永远不会打开的开关后面，只让编译器生成这些实例化。

### 2. 库里不在运行时按值类型实例化泛型

- 内部拼的 IN 条件用 `object[]` 加 `Enumerable.Contains<object>`（`ClientValues`），翻译结果与 `List<T>.Contains` 相同。
- 读取函数：`RuntimeFeature.IsDynamicCodeSupported` 为 false 时，按列读用不带泛型的 `ValueReader.ReadBoxed` 再拆箱；实体的列经 `Setter` 写入。JIT 下仍走按类型直接读、不装箱的路径。
- 列表统一由 `TypedLists.New(元素类型)` 新建，平铺带出的值列表不再用泛型方法。
- 联表条件里一边可空时，转成那一边的类型，不在运行时构造 `Nullable<T>`。

### 3. 编译器看不出来的模板与运算符

`AotTemplates` 在 `EasyDb` 的构造函数里引用（同样不会执行）：

- 1–10 个参数、结果是 `bool` / `object` 的 `Expression<Func<…>>`：内部用 `Expression.Lambda(body, 参数)` 拼的 lambda，委托类型从这些共享模板实例化；
- `decimal`、日期时间、`Guid` 等的公开方法：C# 把表达式树里这些类型的运算编成不带方法的节点，`Expression` 在运行时按反射找 `op_Multiply` 一类方法。

### 4. 其余

- 不再依赖 `MemberInfo.MetadataToken`（Native AOT 没有）：属性按反射返回的顺序（即元数据顺序）。
- 方法映射表里裁剪掉的 BCL 方法跳过：查询里本来写不出没被调用的方法。
- 特性上的类型参数（`ColumnAttribute.Converter`、`KeyGenerator`、`SqlConversion`、`EncryptionAttribute.Strategy`）标了 `DynamicallyAccessedMembers(PublicParameterlessConstructor)`。
- `JsonValueConverter<T>` 新增受保护的构造函数，接受源生成的 `JsonTypeInfo<T>`；二级缓存的 `EasyCacheOptions.Json` 可以给源生成的 `JsonSerializerContext`，没给且反射 JSON 关闭时报错说明怎么改。
- 三个包都打开 `IsAotCompatible`。分析器剩下的报告都核对过，用 `UnconditionalSuppressMessage` 压下，理由集中在 `AotJustification` 的几个常量里。

### 5. 验证

`tests/EasyQuerySharp.Tests.Aot` 把 `docs/examples` 里不限方言或限 SQLite 的文档卡片（183 张）在 SQLite 示例库上逐张执行，每张输出一行结果。`run.sh` 先按 JIT 运行、再按 Native AOT 发布后运行，两次的输出必须逐行一致；发布时的裁剪 / AOT 警告只允许两类：反射版的 `JsonValueConverter`，以及卡片里表达式树的对象初始化、构造、数组（.NET 自己给 `Expression.Bind` / `New` / `NewArrayInit` 的标注）。CI 的 `aot` 任务运行它。

## 不做

- **编译期预编译查询**（EF Core 预编译查询、Dapper.AOT 的做法）：用 interceptors 在编译期翻译查询、生成读取函数。interceptors 在 EF Core 里仍是实验特性；现在 Native AOT 下读取函数解释执行、实体的列经反射写入，比 JIT 慢，有性能需求时再做，记在 BACKLOG。
- **按调用处精确到成员的保留**：生成器整类保留（`DynamicallyAccessedMemberTypes.All`），代价是包大一点，换来不必分析哪些成员会被用到。

## 后果

- 核心、依赖注入、二级缓存三个包可以在裁剪 / Native AOT 的应用里使用，入口不再标 `RequiresUnreferencedCode` / `RequiresDynamicCode`。
- 使用者在 AOT 应用里写表达式树查询时，仍会看到 .NET 对 `Expression.Bind` 等的 IL2026 / IL3050 警告（任何用表达式树的库都一样）；成员已由生成器保留，可以按需压下，见 [裁剪与 Native AOT 指南](../guide/native-aot.md)。
- 驱动本身也要支持 AOT：Microsoft.Data.Sqlite、Npgsql 支持；其余驱动以各自的文档为准。
