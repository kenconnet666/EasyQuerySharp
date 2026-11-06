-- Strings：字符串：BCL 方法直接写
SELECT UPPER(t."title") AS "upper",LENGTH(t."title") AS "length",SUBSTR(t."title",1,2) AS "first",REPLACE(t."title",?,?) AS "replaced",TRIM(? || t."title" || ?) AS "trimmed",(INSTR(t."title",?) - 1) AS "position" FROM "t_post" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: ['查询', 'query', ' ', ' ', 'join', 2]

-- Concat：拼接：+、string.Concat、插值字符串
SELECT rt.* FROM (SELECT t."name" || ? || t."age" || ? FROM "t_user" t WHERE t."age" IS NOT NULL ORDER BY t."id" ASC) rt WHERE ROWNUM < 4;
-- params: ['（', ' 岁）']

-- LikeVariants：包含：Contains 的 % 在参数里，LikeContains 写成 LIKE CONCAT
SELECT t."title" FROM "t_post" t WHERE (t."title" LIKE ('%'||TO_CHAR(?)||'%') OR t."title" LIKE ?) ORDER BY t."id" ASC;
-- params: ['SQL', '%缓存%']

-- Blank：空与空白：string.IsNullOrEmpty / IsNullOrWhiteSpace
SELECT t."user_id" FROM "t_profile" t WHERE (t."website" IS NULL OR t."website" = '') ORDER BY t."user_id" ASC;

-- Dates：日期：取部分、格式化、加减、相差
SELECT cast(to_char(t."created_at",'YYYY') as number) AS "year",cast(to_char(t."created_at",'MM') as number) AS "month",TO_CHAR(t."created_at",'YYYY-MM-DD') AS "day",(t."created_at"+?) AS "next_week",EXTRACT(DAY FROM NUMTODSINTERVAL((t."created_at"+0)-(?+0),'DAY')) AS "days_since_new_year" FROM "t_order" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: [7, '2025-01-01 00:00:00', 2]

-- DateTrunc：日期截断：按月分组
SELECT t1."month" AS "month",t1."orders" AS "orders" FROM (SELECT TRUNC(t."created_at", 'MM') AS "month",COUNT(*) AS "orders" FROM "t_order" t GROUP BY TRUNC(t."created_at", 'MM')) t1 ORDER BY t1."month" ASC;

-- Numbers：数值：Math 的方法直接写
SELECT (t."price" * t."quantity") AS "total",ROUND((t."price" * ?),?) AS "rounded",FLOOR(t."price") AS "floor",CEIL(t."price") AS "ceiling",ABS((t."price" - ?)) AS "abs" FROM "t_order_item" t WHERE t."order_id" = ? ORDER BY t."product_id" ASC;
-- params: [0.85, 1, 100, 7]

-- NullHandling：NULL 处理与一行里取最大：??、NullIf、Sql.MaxColumns
SELECT t."name",NVL(t."age",?) AS "age",NULLIF(t."age", ?) AS "not_zero",(SELECT MAX("__mmc"."__col__") FROM (SELECT NVL(t."age",?) AS "__col__" FROM DUAL UNION ALL SELECT ? AS "__col__" FROM DUAL) "__mmc") AS "larger" FROM "t_user" t WHERE t."id" >= ? AND t."id" <= ? ORDER BY t."id" ASC;
-- params: [0, 17, 0, 30, 5, 7]

-- NullSafeEquals：null 安全的相等：EqualsNullSafe
SELECT t."name" FROM "t_user" t WHERE ((t."age" = ?) OR (t."age" IS NULL AND ? IS NULL));
-- params: [NULL, NULL]

-- CaseWhen：CASE WHEN：三元表达式
SELECT rt.* FROM (SELECT t."title",(CASE WHEN t."views" >= ? THEN ? WHEN t."views" >= ? THEN ? ELSE ? END) AS "heat" FROM "t_post" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 6;
-- params: [2000, '热门', 500, '普通', '冷门']

-- Casts：类型转换：ToString() 写成 CAST
SELECT t."title" || ? || TO_CHAR(t."views") FROM "t_post" t WHERE TO_CHAR(t."views") LIKE ? ORDER BY t."id" ASC;
-- params: ['：', '8%']

-- Json：JSON：Sql.Json(列) 取长度、取元素
-- error: oracle 方言还不支持 Sql.Json 的 JsonLength

-- Windows：窗口函数：ROW_NUMBER 与分区求和
SELECT t."title",t."author_id",(ROW_NUMBER() OVER (PARTITION BY t."author_id" ORDER BY t."views" DESC)) AS "rank",(SUM(t."views") OVER (PARTITION BY t."author_id")) AS "author_views" FROM "t_post" t WHERE t."author_id" <= ? ORDER BY t."author_id" ASC,t."id" ASC;
-- params: [3]

-- WindowFrame：窗口范围：ROWS BETWEEN … AND CURRENT ROW
SELECT rt.* FROM (SELECT t."title",t."views",(SUM(t."views") OVER (ORDER BY t."published_at" ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)) AS "running" FROM "t_post" t WHERE t."published_at" IS NOT NULL ORDER BY t."published_at" ASC) rt WHERE ROWNUM < 5;

-- RawFragment：原生片段：Sql.Raw
SELECT rt.* FROM (SELECT t."name" FROM "t_user" t WHERE LENGTH(t."name") = ? ORDER BY t."id" ASC) rt WHERE ROWNUM < 4;
-- params: [2]

-- CustomFunction：自定义函数：[SqlFunction]
SELECT t."title" FROM "t_post" t WHERE REPLACE(t."title", ' ', '') LIKE ?;
-- params: ['%N+1%']

