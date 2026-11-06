-- FillGeneratedKeys：插入后回填自增主键：FillGeneratedKeys
INSERT INTO [t_tag] ([name]) OUTPUT INSERTED.[id] VALUES (?);
-- params: ['测试']
INSERT INTO [t_tag] ([name]) OUTPUT INSERTED.[id] VALUES (?);
-- params: ['运维']

-- InsertOnlyNotNull：只写非 null 的列：Columns(ColumnStrategy.OnlyNotNull)
INSERT INTO [t_profile] ([user_id],[bio],[skills]) VALUES (?,?,?);
-- params: [5, '做数据的', '[]']

-- Maps：按字典写入：InsertMap / UpdateMap
INSERT INTO [t_tag] ([name]) VALUES (?);
-- params: ['数据库']
UPDATE [t_product] SET [stock] = ? WHERE [id] = ?;
-- params: [150, 6]

-- InsertSelect：INSERT … SELECT：查询结果直接写进另一张表
INSERT INTO [t_tag] ([name]) SELECT eq_src.[name] FROM (SELECT t1.[name] FROM (SELECT t.[name] FROM [t_category] t WHERE t.[parent_id] = ?) t1) eq_src;
-- params: [1]

-- OnConflictIgnore：冲突时忽略：OnConflictIgnore（默认按主键判断冲突）
MERGE INTO [t_post_tag] t1 USING (SELECT ? AS [post_id],? AS [tag_id] ) t2 ON (t1.[post_id] = t2.[post_id] AND t1.[tag_id] = t2.[tag_id]) WHEN NOT MATCHED THEN INSERT ([post_id],[tag_id]) VALUES (t2.[post_id],t2.[tag_id]);;
-- params: [1, 1]

-- OnConflictUpdate：冲突时更新指定的列：OnConflictUpdate
MERGE INTO [t_order_item] t1 USING (SELECT ? AS [order_id],? AS [product_id],? AS [quantity],? AS [price] ) t2 ON (t1.[order_id] = t2.[order_id] AND t1.[product_id] = t2.[product_id]) WHEN MATCHED THEN UPDATE SET t1.[quantity] = t2.[quantity],t1.[price] = t2.[price] WHEN NOT MATCHED THEN INSERT ([order_id],[product_id],[quantity],[price]) VALUES (t2.[order_id],t2.[product_id],t2.[quantity],t2.[price]);;
-- params: [1, 6, 3, 29.90]

-- UpdateSets：按条件更新：Set 单列、SetIf、Increment、SetNull
UPDATE [t_product] SET [stock] = [stock] + ?,[price] = ([price] * ?) WHERE [stock] < ?;
-- params: [10, 0.9, 30]

-- UpdateWhereColumns：按实体更新：换定位列 WhereColumns、跳过几列 IgnoreColumns
SELECT [id],[name],[price],[stock] FROM [t_product] WHERE [id] = ?;
-- params: [6]
UPDATE [t_product] SET [price] = ? WHERE [name] = ?;
-- params: [19.90, '贴纸礼包']

-- UpdateChanges：不追踪时只写改过的列：Changes(before)
SELECT [id],[name],[price],[stock] FROM [t_product] WHERE [id] = ?;
-- params: [6]
UPDATE [t_product] SET [stock] = ? WHERE [id] = ?;
-- params: [195, 6]

-- UpdateBatch：批量更新：Batch()
SELECT [id],[name],[price],[stock] FROM [t_product] WHERE [stock] >= ?;
-- params: [999]
UPDATE [t_product] SET [name] = ?,[price] = ?,[stock] = ? WHERE [id] = ?;
-- params: ['年度会员', 299, 500, 1]
-- params: ['季度会员', 99, 500, 2]

-- UpdateJoin：条件里经对一导航：生成带 JOIN 的 UPDATE
UPDATE t SET t.[status] = ? FROM [t_post] t INNER JOIN [t_user] t1 ON t1.[id] = t.[author_id] WHERE t1.[age] < ?;
-- params: ['draft', 18]

-- ExpectedRows：要求影响的行数：ExecuteAsync(期望行数, 消息)
UPDATE [t_product] SET [stock] = ? WHERE [id] = ?;
-- params: [0, 999]

-- DeleteJoin：条件里经对一导航：生成带 JOIN 的 DELETE
DELETE t FROM [t_comment] t INNER JOIN [t_post] t1 ON t1.[id] = t.[post_id] WHERE t1.[status] = ?;
-- params: ['draft']

-- DeleteEntitiesAndAll：按实体删除与全表删除：All()
SELECT [follower_id],[followee_id] FROM [t_follow] WHERE [follower_id] = ?;
-- params: [1]
DELETE FROM [t_follow] WHERE [follower_id] = ? AND [followee_id] = ?;
-- params: [1, 2]
DELETE FROM [t_follow] WHERE [follower_id] = ? AND [followee_id] = ?;
-- params: [1, 3]
DELETE FROM [t_post_tag];

-- DeleteReturning：删除并返回被删的行：DELETE … RETURNING（PostgreSQL、SQLite）
-- error: mssql 不支持 UPDATE / DELETE … RETURNING

