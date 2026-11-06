-- WhereByIds：按主键查：WhereById / WhereByIds
SELECT t."name" FROM "t_user" t WHERE t."id" IN (?,?);
-- params: [1, 3]

-- RangeAndIn：区间与多值：RangeClosed、In
SELECT t."name" FROM "t_user" t WHERE (t."age" >= ? AND t."age" <= ?) AND t."level" IN (?,?) ORDER BY t."id" ASC;
-- params: [25, 35, 'senior', 'lead']

-- SubQueryConditions：IN 子查询与 EXISTS 子查询
SELECT t."name" FROM "t_user" t WHERE t."id" IN (SELECT t1."author_id" FROM "t_post" t1 WHERE t1."views" >= ?) AND EXISTS(SELECT 1 FROM "t_comment" t2 WHERE t2."author_id" = t."id") ORDER BY t."id" ASC;
-- params: [2000]

-- ScalarSubQuery：标量子查询：Sql.SubQuery
SELECT t."title",t."views",(SELECT AVG(t3."views") FROM "t_post" t3) AS "average" FROM "t_post" t WHERE t."views" > (SELECT AVG(t1."views") FROM "t_post" t1) ORDER BY t."id" ASC;

-- ValueFilter：值为 null 或空白时跳过条件：UseValueFilter
SELECT t."name" FROM "t_user" t LEFT JOIN "t_company" t1 ON t1."id" = t."company_id" WHERE t1."city" = ? ORDER BY t."id" ASC;
-- params: ['杭州']

-- NullsLast：NULL 排在最后：Sql.NullsLast
SELECT rt.* FROM (SELECT t."name",t."age" FROM "t_user" t ORDER BY t."age" DESC NULLS LAST) rt WHERE ROWNUM < 4;

-- OrderByIf：条件成立时才排序：OrderByIf
SELECT rt.* FROM (SELECT t."title" FROM "t_post" t ORDER BY t."views" DESC,t."id" ASC) rt WHERE ROWNUM < 4;

-- SkipTake：跳过与取几行：Skip / Take
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."title" FROM "t_post" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 6) rt1 WHERE rt1."__rownum__" > 2;

-- ReversePaging：深分页反向排序：偏移超过总数一半时倒过来取
SELECT COUNT(*) FROM "t_post";
SELECT rt.* FROM (SELECT t."title" FROM "t_post" t ORDER BY t."id" DESC) rt WHERE ROWNUM < 4;

-- SelectDto：按属性名映射到 DTO：Select<TDto>()
SELECT t."id",t."name",t."age" FROM "t_user" t WHERE t."company_id" = ? ORDER BY t."id" ASC;
-- params: [1]

-- SelectExcept：映射时去掉几列：SelectExcept<TDto>
SELECT t."id",t."name" FROM "t_user" t WHERE t."company_id" = ? ORDER BY t."id" ASC;
-- params: [1]

-- MapPath：导航路径上的值：[MapPath] 平铺、[MapExpression] 额外的列
SELECT t2."name" AS "company_name",(SELECT COUNT(*) FROM "t_comment" t3 WHERE t3."post_id" = t."id") AS "comments",t."title",t."author_id" AS "__relation__authorId",t."id" AS "__relation__id" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" LEFT JOIN "t_company" t2 ON t2."id" = t1."company_id" WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: [3]
SELECT "name","id" AS "__relation__id" FROM "t_user" WHERE "id" IN (?,?);
-- params: [1, 2]
SELECT "post_id","tag_id" FROM "t_post_tag" WHERE "post_id" IN (?,?,?);
-- params: [1, 2, 3]
SELECT "name","id" AS "__relation__id" FROM "t_tag" WHERE "id" IN (?,?);
-- params: [1, 3]

-- ProjectionMethod：可复用投影：[Projection] 方法
SELECT t."id",t."title",t1."name" AS "author" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" WHERE t."views" >= ? ORDER BY t."id" ASC;
-- params: [2000]

-- Without：实体去掉几列：Sql.Without
SELECT t."id",t."name",t."age",t."company_id",t."department_id",t."manager_id" FROM "t_user" t WHERE t."id" <= ?;
-- params: [2]

-- SelectDictionary：读成字典：Select<Dictionary<string, object?>>()
SELECT rt.* FROM (SELECT t."id",t."name",t."city" FROM "t_company" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 3;

-- ClientSide：读出后在客户端换算：Sql.ClientSide
SELECT t."name",t."level" FROM "t_user" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: [3]

-- UnionAll：UNION ALL：结果继续排序、分页
SELECT rt.* FROM (SELECT t2."name",t2."kind" AS "kind" FROM ( (SELECT t."name",? AS "kind" FROM "t_user" t WHERE t."age" < ?)  UNION ALL  (SELECT t1."name",? AS "kind" FROM "t_company" t1 WHERE t1."city" = ?) ) t2 ORDER BY t2."name" ASC) rt WHERE ROWNUM < 4;
-- params: ['用户', 25, '公司', '北京']

-- Cte：CTE：AsCte 之后接着查
WITH "hot_posts" AS (SELECT t."id",t."author_id",t."category_id",t."title",t."status",t."views",t."published_at" FROM "t_post" t WHERE t."views" >= ?) SELECT t2."author_id" AS "author_id",t2."hot" AS "hot" FROM (SELECT t1."author_id" AS "author_id",COUNT(*) AS "hot" FROM "hot_posts" t1 GROUP BY t1."author_id") t2 ORDER BY t2."author_id" ASC;
-- params: [1000]

-- DerivedTable：派生表：投影之后再过滤
SELECT t1."author_id" AS "author_id",t1."hot" AS "hot" FROM (SELECT t."author_id" AS "author_id",COUNT((CASE WHEN t."views" >= ? THEN ? ELSE NULL END)) AS "hot" FROM "t_post" t GROUP BY t."author_id") t1 WHERE t1."hot" >= ? ORDER BY t1."author_id" ASC;
-- params: [1000, 1, 2]

-- Streaming：流式读取：AsAsyncEnumerable 与 ToChunksAsync
SELECT t."name" FROM "t_user" t ORDER BY t."id" ASC;
SELECT rt.* FROM (SELECT "id","name" FROM "t_tag" ORDER BY "id" ASC) rt WHERE ROWNUM < 5;
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT "id","name" FROM "t_tag" ORDER BY "id" ASC) rt WHERE ROWNUM < 9) rt1 WHERE rt1."__rownum__" > 4;

