#!/usr/bin/env bash
# 本仓库开发数据库的 docker compose 包装：首次运行时在 .local/ 生成密码、连接说明与实库测试的连接串，之后原样转发参数。
# 在 WSL 里从仓库任意位置执行：bash infra/dev/db.sh up -d
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
local_dir="$root/.local"
env_file="$local_dir/db.env"

random() { od -An -N9 -tx1 /dev/urandom | tr -d ' \n'; }

if [[ ! -f $env_file ]]; then
  mkdir -p "$local_dir"
  mysql_password="eqs$(random)"
  pg_password="eqs$(random)"
  cat >"$env_file" <<EOF
# 本机开发数据库的密码与 ADO.NET 连接串（勿提交）。测试优先读同名环境变量，没有时读这里。
# 连接串含分号，值加单引号，bash source 时才不会拆开。
EQS_DEV_MYSQL_PASSWORD=$mysql_password
EQS_DEV_PG_PASSWORD=$pg_password

# 本地必测：默认启动的容器
EQS_MYSQL_CS='Server=127.0.0.1;Port=43306;User ID=root;Password=$mysql_password;Database=eqs'
EQS_PG_CS='Host=127.0.0.1;Port=45432;Username=postgres;Password=$pg_password;Database=eqs'

# 按 profile 启动的容器：启动后取消对应 EQS_*_CS 的注释（没启动时留着注释，测试会跳过而不是连接失败）
# MySQL 8.0.18 与 8.4 共用 compose 里 x-mysql 的 root 密码
#EQS_MYSQL8018_CS='Server=127.0.0.1;Port=43318;User ID=root;Password=$mysql_password;Database=eqs'
EOF
  echo "created $env_file"
fi

# 后来加的库：db.env 里缺这个库的密码时补上（不动已有的密码），连接串先写成注释
# 用法：add_db 密码变量 密码后缀 '连接串，{p} 处换成密码'；密码含大小写与数字，后缀用来满足要求符号的库
add_db() {
  grep -q "^$1=" "$env_file" && return
  local password
  password="Eqs$(random)9$2"
  printf '%s=%s\n#%s\n' "$1" "$password" "${3//\{p\}/$password}" >>"$env_file"
}
add_db EQS_DEV_MARIADB_PASSWORD "" "EQS_MARIADB_CS='Server=127.0.0.1;Port=43307;User ID=root;Password={p};Database=eqs'"
# SQL Server 要求带符号；openGauss 只认 #?!@$%^&*- 这几个符号
add_db EQS_DEV_MSSQL_PASSWORD "!" "EQS_MSSQL_CS='Server=127.0.0.1,41433;User ID=sa;Password={p};TrustServerCertificate=True;Initial Catalog=master'"
add_db EQS_DEV_ORACLE_PASSWORD "" "EQS_ORACLE_CS='User Id=eqs;Password={p};Data Source=127.0.0.1:41521/FREEPDB1'"
add_db EQS_DEV_DAMENG_PASSWORD "" "EQS_DAMENG_CS='Server=127.0.0.1;Port=45236;User Id=SYSDBA;PWD={p}'"
add_db EQS_DEV_OPENGAUSS_PASSWORD "-" "EQS_OPENGAUSS_CS='Host=127.0.0.1;Port=45433;Username=gaussdb;Password={p};Database=postgres'"
add_db EQS_DEV_CLICKHOUSE_PASSWORD "" "EQS_CLICKHOUSE_CS='Host=127.0.0.1;Port=48123;Username=eqs;Password={p};Database=eqs'"
add_db EQS_DEV_DB2_PASSWORD "" "EQS_DB2_CS='Server=127.0.0.1:45000;Database=eqs;UID=db2inst1;PWD={p}'"
# HANA 要求 16-64 位且只含字母数字（start.sh 校验）
add_db EQS_DEV_HANA_PASSWORD "" "EQS_HANA_CS='Server=127.0.0.1:49041;UserName=SYSTEM;Password={p}'"


if [[ ! -f $local_dir/connections.md ]]; then
  cat >"$local_dir/connections.md" <<'EOF'
# 本仓库的开发数据库连接（仅本机，勿提交）

密码与 ADO.NET 连接串都在 .local/db.env（EQS_*_CS）。端口统一 4xxxx，与 easy-query-ts-dev（3xxxx）错开。

| 库 | profile | 地址 | 库名 / 服务名 | 用户 |
|---|---|---|---|---|
| MySQL 8.4 | 默认 | 127.0.0.1:43306 | eqs | root |
| PostgreSQL 17 | 默认 | 127.0.0.1:45432 | eqs | postgres |
| MySQL 8.0.18（PolarDB 基线） | polardb | 127.0.0.1:43318 | eqs | root |
| MariaDB 11.4 | mariadb | 127.0.0.1:43307 | eqs | root |
| SQL Server 2022 | mssql | 127.0.0.1:41433 | master | sa |
| Oracle 23ai Free | oracle | 127.0.0.1:41521 | FREEPDB1 | eqs |
| 达梦 DM8 | dameng | 127.0.0.1:45236 | - | SYSDBA |
| openGauss | opengauss | 127.0.0.1:45433 | postgres | gaussdb |
| ClickHouse 25.8 | clickhouse | 127.0.0.1:48123 | eqs | eqs |
| Db2 12.1 | db2 | 127.0.0.1:45000 | eqs | db2inst1 |
| TDengine 3.3 | tdengine | 127.0.0.1:46041 | - | root / taosdata |
| HANA 2.00.088（租户 HXE） | hana | 127.0.0.1:49041 | - | SYSTEM |

启停：bash infra/dev/db.sh up -d ｜ bash infra/dev/db.sh --profile mssql up -d ｜ bash infra/dev/db.sh stop
EOF
fi


exec docker compose -f "$here/compose.yaml" --env-file "$env_file" "$@"
