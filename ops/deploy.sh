#!/usr/bin/env bash
# ================================================================
# deploy.sh — deploy the Daily Report Service on the production box.
#
# Same shape as the two game deploys: always MAIN, a per-commit image tag so
# rollback is "run the previous sha", log rotation, a prune scoped to our own
# label. Differences: no inbound port (nothing connects to this), a named
# volume for the SQLite state and the report archive, and no /healthz to wait
# for — the smoke test is a dry run of the report itself, printed to your
# terminal, which proves config, secrets, database, GoatCounter and probes in
# one go without sending anything.
#
# Install on the box (once, and again whenever this file changes):
#   cp ~/DailyReportService/ops/deploy.sh ~/deploy-dr.sh && chmod +x ~/deploy-dr.sh
#
# A restart drops nothing: the schedule is recomputed on start, and a missed
# 07:00 slot is caught up immediately (once per report date, never twice).
# ================================================================
set -euo pipefail

REPO="$HOME/DailyReportService"
NAME="dr"
ENV_FILE="/etc/dailyreport/.env"
VOLUME="dr-data"
cd "$REPO"

if [[ ! -r "$ENV_FILE" ]]; then
  echo "✗ $ENV_FILE is missing or unreadable — see docs/DEPLOY.md §1"
  exit 1
fi

echo "── Updating to main ──"
git fetch origin main
git checkout main
git pull --ff-only origin main

SHA="$(git rev-parse --short HEAD)"
PREV="$(docker inspect --format '{{.Config.Image}}' "$NAME" 2>/dev/null || echo 'none')"
echo "── Building ${NAME}:${SHA} (previously running: ${PREV}) ──"
# --pull refreshes the .NET base images so their Debian patches actually arrive.
docker build --pull --label app=dailyreport -t "${NAME}:${SHA}" -t "${NAME}:latest" .

echo "── Restarting ──"
docker rm -f "$NAME" 2>/dev/null || true
docker run -d --name "$NAME" --restart=always \
  --log-opt max-size=10m --log-opt max-file=3 \
  --env-file "$ENV_FILE" \
  -v "${VOLUME}:/data" \
  "${NAME}:${SHA}"

echo "── Waiting for the scheduler to start ──"
for i in $(seq 1 20); do
  if docker logs "$NAME" 2>&1 | grep -q "Next report at"; then
    break
  fi
  if [[ "$(docker inspect --format '{{.State.Running}}' "$NAME")" != "true" ]]; then
    echo "✗ Container exited — logs:"
    docker logs --tail 60 "$NAME" || true
    echo "Rollback: docker rm -f ${NAME} && docker run -d --name ${NAME} --restart=always --env-file ${ENV_FILE} -v ${VOLUME}:/data ${PREV}"
    exit 1
  fi
  sleep 1
done
docker logs --tail 5 "$NAME"

echo
echo "── Smoke test: dry run of today's report (nothing is sent) ──"
set +e
docker exec "$NAME" dotnet DailyReport.Worker.dll --once --dry-run
rc=$?
set -e

prune_old_images() {
  # `docker image prune` never touches tagged images, so old dr:<sha> tags would pile up for ever on the
  # shared box. Keep :latest, the one just deployed, and the newest previous one (the rollback target).
  docker image prune -f --filter "label=app=dailyreport" > /dev/null
  docker image ls "$NAME" --format '{{.Repository}}:{{.Tag}}' \
    | grep -v -e ':latest$' -e ":${SHA}$" \
    | tail -n +2 \
    | xargs -r docker rmi > /dev/null 2>&1 || true
}

case "$rc" in
  0)
    echo
    echo "✓ Deployed ${NAME}:${SHA} (main). The dry run above is all green."
    ;;
  2)
    echo
    echo "⚠ Deployed ${NAME}:${SHA} (main), but the dry run above has red lines. Each one is either a real"
    echo "  finding about a game (go look) or a missing secret in ${ENV_FILE} (fix it and redeploy)."
    echo "  The container is live and will send at 07:00 either way."
    ;;
  *)
    echo "✗ The dry run failed (exit ${rc}) — the container is live but the report cannot be produced. Logs:"
    docker logs --tail 60 "$NAME" || true
    exit 1
    ;;
esac

echo "  To send now instead of waiting for 07:00:  docker exec ${NAME} dotnet DailyReport.Worker.dll --once"
prune_old_images
exit 0
