-- FirstMissing：查不到时返回 null
SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "name" = ? AND ROWNUM < 2;
-- params: ['不存在的人']
SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "id" = ?;
-- params: [999]

-- FindThrows：查不到时抛 NotFoundException
SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "id" = ?;
-- params: [999]

-- DynamicTable：无实体的表：按列名查询
SELECT rt.* FROM (SELECT "name" AS "name","age" AS "age" FROM "t_user" WHERE "age" >= ? ORDER BY "age" ASC) rt WHERE ROWNUM < 4;
-- params: [30]

-- DepartmentTree：树：取全部下级并拼成树
WITH "as_tree_cte" ("cte_deep","id","company_id","parent_id","name") AS ( (SELECT 0 AS "cte_deep",t1."id",t1."company_id",t1."parent_id",t1."name" FROM "t_department" t1 WHERE t1."name" = ?)  UNION ALL  (SELECT t2."cte_deep" + 1 AS "cte_deep",t3."id",t3."company_id",t3."parent_id",t3."name" FROM "as_tree_cte" t2 INNER JOIN "t_department" t3 ON t3."parent_id" = t2."id") ) SELECT t."id",t."company_id",t."parent_id",t."name",t."cte_deep" FROM "as_tree_cte" t;
-- params: ['研发部']

-- CountAndAny：按条件计数与判断存在
SELECT COUNT(*) FROM "t_user" WHERE "age" >= ?;
-- params: [18]
SELECT  1  FROM "t_post" WHERE "status" = ? AND ROWNUM < 2;
-- params: ['draft']

-- SelectExpressions：选表达式
SELECT UPPER(t."title") AS "title",(t."views" + ?) AS "next_views",(CASE WHEN (t."views" >= ?) THEN ? ELSE ? END) AS "is_hot" FROM "t_post" t WHERE t."status" = ? AND ROWNUM < 4;
-- params: [1, 2000, true, false, 'published']

-- SelectNavigation：经导航取关联表的列
SELECT rt.* FROM (SELECT t."title",t."views",t1."name" AS "author",t2."name" AS "company" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" LEFT JOIN "t_company" t2 ON t2."id" = t1."company_id" ORDER BY t."id" ASC) rt WHERE ROWNUM < 3;

-- PageThenProject：先分页再投影到 DTO：导航只对这一页带出
SELECT COUNT(*) FROM "t_post";
SELECT t1."title",t1."author_id" AS "__relation__authorId" FROM (SELECT rt.* FROM (SELECT t."id",t."author_id",t."category_id",t."title",t."status",t."views",t."published_at" FROM "t_post" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 3) t1;
SELECT t."name",t."id" AS "__relation__id" FROM "t_user" t WHERE t."id" IN (?);
-- params: [1]

-- InCsv：值在逗号分隔的列表里：Sql.InCsv
SELECT t."title" FROM "t_post" t WHERE (INSTR(',' || ? || ',', ',' || t."id" || ',') > 0) ORDER BY t."id" ASC;
-- params: ['1,3']

-- PreviousRow：窗口函数：按查询的排序取上一行
SELECT t."title",t."views",LAG(t."views", 1, ?) OVER (ORDER BY t."published_at" ASC,t."id" ASC) AS "previous_views" FROM "t_post" t ORDER BY t."published_at" ASC,t."id" ASC;
-- params: [0]

-- ValueObjects：值对象：按成员过滤、整体投影
SELECT t."name",t."geo_lat",t."geo_lng" FROM "t_shop" t WHERE t."address_city" = ? AND t."address_street" IS NOT NULL;
-- params: ['杭州']

-- FlattenNavigations：沿集合导航展开
SELECT DISTINCT t3."name" FROM "t_company" t INNER JOIN "t_user" t1 ON t1."company_id" = t."id" INNER JOIN "t_follow" t2 ON t2."follower_id" = t1."id" INNER JOIN "t_user" t3 ON t3."id" = t2."followee_id" WHERE t."id" = ? ORDER BY t3."name" ASC;
-- params: [1]

-- DistinctRows：去重
SELECT rt.* FROM (SELECT t1."author_id",t1."status" FROM (SELECT DISTINCT t."author_id",t."status" FROM "t_post" t) t1 ORDER BY t1."author_id" ASC) rt WHERE ROWNUM < 5;

-- WhereChain：多个 Where 之间是 AND
SELECT t."name" FROM "t_user" t WHERE t."age" >= ? AND t."company_id" = ?;
-- params: [18, 1]

-- CompareColumns：两列比较：作者自己的评论
SELECT t."body",t2."name" AS "by" FROM "t_comment" t LEFT JOIN "t_post" t1 ON t1."id" = t."post_id" LEFT JOIN "t_user" t2 ON t2."id" = t."author_id" WHERE t."author_id" = t1."author_id";

-- LikeFamily：LIKE 家族
SELECT t."title" FROM "t_post" t WHERE (t."title" LIKE ? OR t."title" LIKE ? OR t."title" LIKE ?);
-- params: ['%缓存%', '把%', '%入门']

-- LikeLiteral：值里的通配符按字面匹配
SELECT t."title" FROM "t_post" t WHERE t."title" LIKE ?;
-- params: ['%N+1%']

-- InList：IN 与 NOT IN
SELECT t."name" FROM "t_user" t WHERE t."company_id" IN (?,?) AND t."level" NOT IN (?);
-- params: [1, 3, 'lead']

-- NullChecks：可空列与 NULL
SELECT t."name" FROM "t_user" t WHERE (t."age" IS NULL OR t."company_id" IS NULL);

-- WhereIf：条件成立时才加的 Where
SELECT t."title" FROM "t_post" t WHERE t."title" LIKE ?;
-- params: ['%数据%']

-- ProjectMap：可复用投影：Project(map)
SELECT rt.* FROM (SELECT t1."name",t1."age" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" WHERE t."status" = ? ORDER BY t."id" ASC) rt WHERE ROWNUM < 4;
-- params: ['published']

-- OrderByColumns：多列排序
SELECT t."name",t."age" FROM "t_user" t ORDER BY t."level" DESC,t."age" ASC;

-- Paging：分页：总数与当前页
SELECT COUNT(*) FROM "t_post" WHERE "status" = ?;
-- params: ['published']
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."title" FROM "t_post" t WHERE t."status" = ? ORDER BY t."views" DESC) rt WHERE ROWNUM < 9) rt1 WHERE rt1."__rownum__" > 4;
-- params: ['published']

