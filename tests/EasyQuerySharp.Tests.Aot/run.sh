#!/usr/bin/env bash
# 同一个冒烟程序先按 JIT 运行、再按 Native AOT 发布后运行，两次的输出必须逐行一致；发布时的裁剪 / AOT 警告只允许项目文件里说明的两类。
# 需要 Native AOT 的工具链（Linux 上是 clang 与 zlib 开发包）。用法：bash tests/EasyQuerySharp.Tests.Aot/run.sh [输出目录]
set -euo pipefail

project=tests/EasyQuerySharp.Tests.Aot
out=${1:-artifacts/aot}
mkdir -p "$out"

dotnet run -c Release --project "$project" -- "$out/jit.txt"
# 警告不当错误：下面按来源核对
dotnet publish "$project" -c Release -o "$out/bin" -p:TreatWarningsAsErrors=false | tee "$out/publish.log"
"$out/bin/EasyQuerySharp.Tests.Aot" "$out/aot.txt"

# 允许的：冒烟程序自己的反射（Program）；示例模型与卡片里反射版的 JsonValueConverter；卡片的表达式树里
# 对象初始化、构造与数组编译出来的 Expression.Bind / New / NewArrayInit（.NET 自己的标注，使用者的查询都会有，成员由生成器保留）
unexpected=$(grep -E '(warning|error) IL[0-9]+' "$out/publish.log" | grep -vE 'Program\.<|JsonValueConverter|System\.Linq\.Expressions\.Expression\.(Bind|New|NewArrayInit)\(' || true)
if [[ -n $unexpected ]]; then
  echo "::error::Native AOT 发布出现了意外的裁剪 / AOT 警告"
  echo "$unexpected"
  exit 1
fi
tail -n 1 "$out/aot.txt"
diff "$out/jit.txt" "$out/aot.txt"
