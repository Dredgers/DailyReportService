# Daily Report Service — one container, no inbound port, one email a day.
# Build:  docker build -t dr:local .
# Run:    docker run --rm --env-file .env -v dr-data:/data dr:local --once --dry-run

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props DailyReportService.slnx ./
COPY src/DailyReport.Core/DailyReport.Core.csproj src/DailyReport.Core/
COPY src/DailyReport.Infrastructure/DailyReport.Infrastructure.csproj src/DailyReport.Infrastructure/
COPY src/DailyReport.Worker/DailyReport.Worker.csproj src/DailyReport.Worker/
RUN dotnet restore src/DailyReport.Worker/DailyReport.Worker.csproj
COPY src/ src/
RUN dotnet publish src/DailyReport.Worker/DailyReport.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
# IANA zones (Europe/Copenhagen) need tzdata; ICU is already in the Debian runtime image.
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /data && chown app:app /data
ENV DOTNET_ENVIRONMENT=Production \
    Report__StateDirectory=/data \
    TZ=UTC
VOLUME ["/data"]
USER app
COPY --from=build /app .
HEALTHCHECK --interval=30m --timeout=20s --start-period=1m --retries=3 \
  CMD ["dotnet", "DailyReport.Worker.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "DailyReport.Worker.dll"]
