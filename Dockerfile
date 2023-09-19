FROM mcr.microsoft.com/dotnet/framework/sdk:4.7.2

WORKDIR /app

COPY FontDumpTruck/ .
RUN dotnet restore
RUN dotnet build -c Release -o out
ENTRYPOINT ["/app/out/FontDumpTruck.exe"]
