#!/usr/bin/env bash
# push_with_retry.sh - 推送 LFS 对象和 git 引用，自动重试

set -e
REMOTE="origin"
BRANCH="master"
MAX_RETRIES=50
SLEEP_SEC=5

echo "========================================="
echo "开始推送 LFS 对象 + Git 引用"
echo "远程: $REMOTE / 分支: $BRANCH"
echo "最大重试次数: $MAX_RETRIES"
echo "========================================="

# 第一步：推送 LFS 对象
count=1
while [ $count -le $MAX_RETRIES ]; do
  echo ""
  echo "=== [LFS] 第 ${count} 次尝试推送 LFS 对象 ==="
  output=$(git lfs push --all "$REMOTE" "$BRANCH" 2>&1) && rc=0 || rc=$?
  echo "$output"

  if [ $rc -eq 0 ]; then
    echo "✅ LFS 对象推送成功！"
    break
  fi

  if [ $count -ge $MAX_RETRIES ]; then
    echo "❌ LFS 推送已达最大重试次数(${MAX_RETRIES})，放弃"
    exit 1
  fi

  echo "⚠️ LFS 推送失败(退出码:${rc})，${SLEEP_SEC}秒后重试..."
  sleep $SLEEP_SEC
  count=$((count+1))
done

# 第二步：推送 Git 引用
count=1
while [ $count -le $MAX_RETRIES ]; do
  echo ""
  echo "=== [GIT] 第 ${count} 次尝试推送 Git 引用 ==="
  output=$(git push "$REMOTE" "$BRANCH" 2>&1) && rc=0 || rc=$?
  echo "$output"

  if [ $rc -eq 0 ]; then
    echo "✅ Git 引用推送成功！"
    echo ""
    echo "🎉 全部推送完成！"
    break
  fi

  if echo "$output" | grep -qi "Everything up-to-date"; then
    echo "✅ 已经是最新，无需推送"
    break
  fi

  if [ $count -ge $MAX_RETRIES ]; then
    echo "❌ Git 推送已达最大重试次数(${MAX_RETRIES})，放弃"
    exit 1
  fi

  echo "⚠️ Git 推送失败(退出码:${rc})，${SLEEP_SEC}秒后重试..."
  sleep $SLEEP_SEC
  count=$((count+1))
done
