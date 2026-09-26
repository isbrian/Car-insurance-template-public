FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CarInsurance.Api.csproj", "./"]
RUN dotnet restore "CarInsurance.Api.csproj"
COPY . .
RUN dotnet publish "CarInsurance.Api.csproj" -c Release -o /app/publish -r linux-x64 --self-contained false /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN useradd --create-home --uid 10001 appuser \
    && mkdir -p /data \
    && chown -R appuser:appuser /app /data
COPY --from=build /app/publish .
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CarInsurance.Api.dll"]
