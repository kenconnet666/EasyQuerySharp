#!/usr/bin/env bash
# HANA 数据卷的快照。首次启动要初始化系统库、建租户库（单线程，各一两分钟，CI 上整个启动约 7 分钟）；带着初始化好的数据卷启动只要半分钟。
# 把初始化好的卷存成一个包，之后直接恢复（CI 按它做缓存，本机想回到刚初始化的状态时也用它）：
#   bash infra/dev/hana/volume.sh save hana-volume.tar.gz     # 停掉 HANA（等它写完保存点）再打包；之后要用得重新 up
#   bash infra/dev/hana/volume.sh restore hana-volume.tar.gz  # 建好容器与卷、把包解进去，不启动；之后 up 即可
# 卷有将近 4 GB，大半是预分配的日志段（全是零），压缩后约 100 MB。做法照 easy-query-ts 的 infra/dev/hana/volume.sh。
# 快照里有 SYSTEM 用户的密码与实例的密钥：只能和打包时同一个 EQS_DEV_HANA_PASSWORD、同一个镜像一起用。
set -euo pipefail

action=${1:?save or restore}
file=$(realpath -m "${2:?path of the .tar.gz file}")
here=$(cd "$(dirname "$0")" && pwd)
dc() { bash "$here/../db.sh" --profile hana "$@"; }

# 在一个临时容器里以 root 打包 / 解包：卷里的文件属于 hxeadm（12000:79），数字属主要原样保留
tar_in_volume() {
  local container image
  container=$(dc ps -aq hana)
  image=$(docker inspect -f '{{.Config.Image}}' "$container")
  docker run --rm --volumes-from "$container" -v "$(dirname "$file"):/eq-snapshot" -u 0 --entrypoint tar \
    "$image" -C /hana/mounts "$@"
}

case $action in
  save)
    # 给够时间让 HANA 写完保存点再退出
    dc stop -t 120 hana
    mkdir -p "$(dirname "$file")"
    # gzip -1：内容大多是零，最快的一档就够
    tar_in_volume -I 'gzip -1' -cf "/eq-snapshot/$(basename "$file")" .
    ;;
  restore)
    dc up --no-start --build hana
    tar_in_volume -xzpf "/eq-snapshot/$(basename "$file")"
    ;;
  *)
    echo "unknown action '$action': save or restore" >&2
    exit 1
    ;;
esac
