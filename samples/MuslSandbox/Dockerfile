FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine

RUN apk add --no-cache bash build-base git ncurses-terminfo python3

ENV TERM=xterm-256color \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=true

WORKDIR /repo
COPY . .

CMD ["/bin/bash"]
