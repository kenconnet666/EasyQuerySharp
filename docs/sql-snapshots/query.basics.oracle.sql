-- FirstMissing：查不到时返回 null
SELECT rt.* FROM (SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "name" = ?) rt WHERE ROWNUM < 2;
-- params: ['不存在的人']
SELECT rt.* FROM (SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "id" = ?) rt WHERE ROWNUM < 2;
-- params: [999]

-- CountAndAny：按条件计数与判断存在
SELECT COUNT(*) FROM "t_user" WHERE "age" >= ?;
-- params: [18]
SELECT rt.* FROM (SELECT  1  FROM "t_post" WHERE "status" = ?) rt WHERE ROWNUM < 2;
-- params: ['draft']

-- SelectExpressions：选表达式
SELECT rt.* FROM (SELECT UPPER(t."title") AS "title",(t."views" + ?) AS "next_views",(t."views" >= ?) AS "is_hot" FROM "t_post" t WHERE t."status" = ?) rt WHERE ROWNUM < 4;
-- params: [1, 2000, 'published']

-- SelectNavigation：经导航取关联表的列
SELECT rt.* FROM (SELECT t."title",t."views",t1."name" AS "author",t2."name" AS "company" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" LEFT JOIN "t_company" t2 ON t2."id" = t1."company_id" ORDER BY t."id" ASC) rt WHERE ROWNUM < 3;

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

-- OrderByColumns：多列排序
SELECT t."name",t."age" FROM "t_user" t ORDER BY t."level" DESC,t."age" ASC;

-- Paging：分页：总数与当前页
SELECT COUNT(*) FROM "t_post" WHERE "status" = ?;
-- params: ['published']
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."title" FROM "t_post" t WHERE t."status" = ? ORDER BY t."views" DESC) rt WHERE ROWNUM < 9) rt1 WHERE rt1."__rownum__" > 4;
-- params: ['published']

