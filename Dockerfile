FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
LABEL "author"="Mary <contact@mary.zone>"
WORKDIR /source

ARG TARGETARCH

# Install NativeAOT build prerequisites
RUN apk add --no-cache clang gcc musl-dev zlib-dev

# Map docker arch to .NET RID
RUN case "$TARGETARCH" in \
        amd64) echo "linux-musl-x64"   > /tmp/rid ;; \
        arm64) echo "linux-musl-arm64" > /tmp/rid ;; \
        *)     echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac

COPY *.csproj .
RUN dotnet restore -r "$(cat /tmp/rid)"

COPY . .
RUN dotnet publish -c release -o /app -r "$(cat /tmp/rid)" --no-restore LanPlayServer.csproj

FROM alpine:latest
WORKDIR /app
COPY --from=build /app .
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh

# See: https://github.com/dotnet/announcements/issues/20
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    IP_BAN_FILE_PATH=/data/ryuldn/bannedips.txt

RUN apk add --no-cache icu-libs \
    && addgroup -S appgroup && adduser -S appuser -G appgroup \
    && mkdir -p /data/ryuldn && touch /data/ryuldn/bannedips.txt \
    && chown -R appuser:appgroup /app /data \
    && chmod +x /usr/local/bin/docker-entrypoint.sh
USER appuser

EXPOSE 30456
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
