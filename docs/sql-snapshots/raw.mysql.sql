-- SqlQuery：插值字符串：插值处都是参数，集合展开成多个参数
SELECT name, age FROM t_user WHERE age >= ? AND company_id IN (?,?) ORDER BY id;
-- params: [30, 1, 2]

-- ScalarAndIdentifier：读单列与标识符：Sql.Identifier 按方言加引号
SELECT COUNT(*) FROM `t_post` WHERE `views` >= ?;
-- params: [1000]

-- ExecuteSql：执行写入：ExecuteSqlAsync 返回影响的行数
UPDATE t_product SET stock = stock - ? WHERE id = ?;
-- params: [1, 5]

-- Positional：? 占位的位置参数
SELECT name FROM t_tag WHERE id > ? ORDER BY id;
-- params: [4]

-- FromSql：原生 SQL 当作查询来源：FromSql 之后继续 Where / Select
SELECT t.`name` FROM (SELECT * FROM t_user WHERE age >= ?) t WHERE t.`company_id` = ? ORDER BY t.`id` ASC;
-- params: [30, 1]

-- RawOrderAndSelect：原生排序与选列：OrderByRaw、SelectRaw
SELECT id, title, views FROM `t_post` WHERE `views` > ? ORDER BY views % 7 LIMIT 3;
-- params: [0]

-- ReadAsync：直接读 DbDataReader：ReadAsync
SELECT t.`name` FROM `t_company` t ORDER BY t.`id` ASC;

-- ToSql：只生成 SQL 不执行：ToSql / ToStatement

