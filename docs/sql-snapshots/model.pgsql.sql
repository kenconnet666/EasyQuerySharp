-- EnumByName：枚举按名字存：条件里的值也换成名字
SELECT t."name" FROM "t_user" t WHERE t."level" = ? ORDER BY t."id" ASC;
-- params: ['lead']

-- JsonColumn：JSON 列：读出来就是对象
SELECT t."user_id",t."skills" FROM "t_profile" t WHERE t."website" IS NOT NULL ORDER BY t."user_id" ASC;

-- ValueObjectInsert：值对象：插入时展开成几列
INSERT INTO "t_shop" ("name","address_city","address_street","geo_lat","geo_lng") VALUES (?,?,?,?,?);
-- params: ['鼓楼店', '南京', '中山路 1 号', 32.06, 118.78]

-- ValueObjectUpdate：值对象：整个或只写其中一列
UPDATE "t_shop" SET "address_city" = ?,"address_street" = ? WHERE "id" = ?;
-- params: ['杭州', '江南大道 3 号', 2]
UPDATE "t_shop" SET "geo_lat" = ? WHERE "id" = ?;
-- params: [30.2, 2]

-- ComputedColumn：计算列：查询时就地展开，条件里也能用
SELECT "id","title","views",("views" / ?) AS "kilo_views" FROM "t_post" WHERE ("views" / ?) >= ? ORDER BY "id" ASC;
-- params: [1000, 1000, 2]

-- NotAutoSelected：AutoSelect = false：查实体时不带这一列，显式选择才查
SELECT "user_id","website" FROM "t_profile" WHERE "user_id" <= ?;
-- params: [2]
SELECT t."user_id",t."bio" FROM "t_profile" t WHERE t."user_id" <= ?;
-- params: [2]

