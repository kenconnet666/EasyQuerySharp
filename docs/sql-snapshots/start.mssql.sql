-- FirstQuery：条件、隐式 JOIN、排序、投影
SELECT t.[name],t.[age],t1.[name] AS [company] FROM [t_user] t LEFT JOIN [t_company] t1 ON t1.[id] = t.[company_id] WHERE t.[age] >= ? AND t1.[city] = ? ORDER BY t.[name] ASC;
-- params: [30, '杭州']

-- CollectionNavigation：隐式子查询与对象树：集合导航上的 Any / Count / Select
SELECT t.[name],(SELECT COUNT(*) FROM [t_user] t2 WHERE t2.[company_id] = t.[id]) AS [headcount],t.[id] AS [__relation__id] FROM [t_company] t WHERE EXISTS(SELECT TOP 1 1 FROM [t_user] t1 WHERE t1.[company_id] = t.[id] AND t1.[age] < ?) ORDER BY t.[id] ASC;
-- params: [30]
SELECT t.[name],t.[company_id] AS [__relation__companyId] FROM [t_user] t WHERE t.[company_id] IN (?,?,?) ORDER BY t.[id] ASC;
-- params: [1, 2, 3]

-- Readme：README 的例子：隐式 JOIN、隐式子查询、对象树、分页
SELECT COUNT(*) FROM [t_user] t LEFT JOIN [t_company] t1 ON t1.[id] = t.[company_id] WHERE t1.[city] = ? AND EXISTS(SELECT TOP 1 1 FROM [t_post] t2 WHERE t2.[author_id] = t.[id] AND t2.[views] >= ?);
-- params: ['杭州', 1000]
SELECT TOP 3 t.[name],t1.[name] AS [company],(SELECT COUNT(*) FROM [t_post] t3 WHERE t3.[author_id] = t.[id]) AS [posts],t.[id] AS [__relation__id] FROM [t_user] t LEFT JOIN [t_company] t1 ON t1.[id] = t.[company_id] WHERE t1.[city] = ? AND EXISTS(SELECT TOP 1 1 FROM [t_post] t2 WHERE t2.[author_id] = t.[id] AND t2.[views] >= ?) ORDER BY t.[name] ASC;
-- params: ['杭州', 1000]
SELECT t.[title],t.[author_id] AS [__relation__authorId] FROM [t_post] t WHERE t.[author_id] IN (?,?,?) ORDER BY t.[views] DESC;
-- params: [2, 1, 7]

-- FirstPage：分页
SELECT COUNT(*) FROM [t_user];
SELECT TOP 3 t.[name] FROM [t_user] t ORDER BY t.[id] ASC;

-- Writes：插入、更新、删除
INSERT INTO [t_company] ([name],[city]) VALUES (?,?);
-- params: ['东海数据', '宁波']
UPDATE [t_company] SET [city] = ? WHERE [name] = ?;
-- params: ['舟山', '东海数据']
DELETE FROM [t_company] WHERE [name] = ?;
-- params: ['东海数据']

