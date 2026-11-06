-- AsTable：这条查询的主表换成另一张：AsTable
CREATE TABLE t_tag_2025 AS SELECT * FROM t_tag;
SELECT t.[name] FROM [t_tag_2025] t WHERE t.[id] <= ? ORDER BY t.[id] ASC;
-- params: [3]
DROP TABLE t_tag_2025;

-- AsTableByName：按原表名推出新表名；导航上也能换：Sql.AsTable
CREATE TABLE t_user_archive AS SELECT * FROM t_user;
SELECT t.[name] FROM [t_user_archive] t WHERE t.[age] >= ?;
-- params: [35]
SELECT t.[title] FROM [t_post] t LEFT JOIN [t_user_archive] t1 ON t1.[id] = t.[author_id] WHERE t1.[age] >= ? ORDER BY t.[id] ASC;
-- params: [35]
DROP TABLE t_user_archive;

-- AsTableSegment：整个表引用自己写：AsTableSegment
SELECT t.[title] FROM (SELECT * FROM [t_post] WHERE status = 'published') t WHERE t.[views] >= ? ORDER BY t.[id] ASC;
-- params: [2000]

-- WriteToOtherTable：写入时换表、换 schema
CREATE TABLE t_tag_archive AS SELECT * FROM t_tag WHERE 1 = 0;
INSERT INTO [t_tag_archive] ([name]) VALUES (?);
-- params: ['归档']
UPDATE [main].[t_tag] SET [name] = ? WHERE [id] = ?;
-- params: ['ORM 框架', 1]
DROP TABLE t_tag_archive;

