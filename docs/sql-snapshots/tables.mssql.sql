-- MapTable：换表：条件与投影里的集合导航跟着读归档表
CREATE TABLE t_post_2025 AS SELECT * FROM t_post WHERE id <= 2;
SELECT t.[name],(SELECT COUNT(*) FROM [t_post_2025] t2 WHERE t2.[author_id] = t.[id]) AS [posts] FROM [t_user] t WHERE EXISTS(SELECT TOP 1 1 FROM [t_post_2025] t1 WHERE t1.[author_id] = t.[id] AND t1.[views] >= ?) ORDER BY t.[id] ASC;
-- params: [100]
DROP TABLE t_post_2025;

-- InSchema：整条查询换 schema：主表与隐式 JOIN 都放到同一个 schema 下
SELECT t.[title],t1.[name] AS [author] FROM [main].[t_post] t LEFT JOIN [main].[t_user] t1 ON t1.[id] = t.[author_id] WHERE t.[views] >= ? ORDER BY t.[id] ASC;
-- params: [1000]

-- TableLink：改写表前面的连接符：LEFT JOIN 写成 LEFT OUTER JOIN
SELECT t.[title],t1.[name] AS [author] FROM [t_post] t LEFT OUTER JOIN [t_user] t1 ON t.[author_id] = t1.[id] WHERE t.[views] >= ? ORDER BY t.[id] ASC;
-- params: [1000]

