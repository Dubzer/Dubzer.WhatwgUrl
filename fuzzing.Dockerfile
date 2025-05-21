FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

RUN dotnet tool install --global SharpFuzz.CommandLine
ENV PATH="$PATH:/root/.dotnet/tools"
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

WORKDIR /src
COPY ./src/Dubzer.WhatwgUrl ./Dubzer.WhatwgUrl
COPY ./src/Dubzer.WhatwgUrl.Fuzzing ./Dubzer.WhatwgUrl.Fuzzing
COPY ./Directory.Build.props .
RUN dotnet publish -c Release ./Dubzer.WhatwgUrl.Fuzzing/Dubzer.WhatwgUrl.Fuzzing.csproj -o /app

COPY ./src/Dubzer.WhatwgUrl.Fuzzing/Seeds.txt /app/Seeds/
WORKDIR /app/Seeds/
RUN split -l 1 Seeds.txt seed_ && rm Seeds.txt

WORKDIR /app
RUN sharpfuzz ./Dubzer.WhatwgUrl.dll

FROM aflplusplus/aflplusplus:latest
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 9.0 && \
    rm dotnet-install.sh

ENV PATH="$PATH:/root/.dotnet"
ENV AFL_SKIP_BIN_CHECK=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=build /app /app
WORKDIR /app
ENTRYPOINT ["afl-fuzz", "-i", "./Seeds", "-o", "./Fuzzer", "-t", "10000", "-m", "none", "dotnet", "./Dubzer.WhatwgUrl.Fuzzing.dll"]