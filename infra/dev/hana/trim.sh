#!/usr/bin/env bash
# 启动完成（run_hana 写出 /hana/.ready，SAP 的 post_start 钩子已跑完）后关掉测试用不到的服务，使 HANA 能在 compose 的 mem_limit 里运行。
# 每次启动都执行：hxe_optimize（首次启动）会重新打开统计服务与 diserver，web dispatcher 每次启动又被设回 1。
# 全局内存上限不另设：HANA 按容器的 cgroup 上限（compose 的 mem_limit）取 90%，限制只写在 compose 一处。
set -euo pipefail

password=${EQ_HANA_PASSWORD:?Set EQ_HANA_PASSWORD}
unset EQ_HANA_PASSWORD
until [[ -f /hana/.ready ]]; do sleep 5; done

sql() { /usr/sap/HXE/HDB90/exe/hdbsql -n 127.0.0.1:39017 -d SYSTEMDB -u SYSTEM -p "$password" "$1" >/dev/null; }
# web dispatcher（HTTP 入口）与 diserver（HDI 部署）只给 XS / HDI 用；嵌入式统计服务每分钟采集一次监控数据
sql "ALTER SYSTEM ALTER CONFIGURATION ('daemon.ini','HOST','hxehost') SET ('webdispatcher','instances') = '0', ('diserver','instances') = '0' WITH RECONFIGURE"
sql "ALTER SYSTEM ALTER CONFIGURATION ('nameserver.ini','SYSTEM') SET ('statisticsserver','active') = 'false' WITH RECONFIGURE"
touch /run/eq/trimmed
echo 'eq: webdispatcher, diserver and the statistics service are off'
