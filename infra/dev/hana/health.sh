#!/usr/bin/env bash
set -euo pipefail

# trim.sh 关掉多余的服务之后才算就绪，测试不会在裁剪前后的重新配置中途连进来
[[ -f /run/eq/trimmed ]]
/usr/sap/HXE/HDB90/exe/hdbsql -n 127.0.0.1:39017 -d HXE -u SYSTEM \
  -p "${EQ_HANA_PASSWORD:?Set EQ_HANA_PASSWORD}" 'SELECT 1 FROM DUMMY' >/dev/null 2>&1
rm -f /run/eq/password.json
