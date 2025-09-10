# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the entire solution
COPY . .

# Restore & publish
WORKDIR /src/FinanceMaker.Worker
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5001

ENTRYPOINT ["dotnet", "FinanceMaker.Worker.dll"]