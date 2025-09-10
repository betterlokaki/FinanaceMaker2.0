# -------------------------
# Stage 1: Build .NET App
# -------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy all project files
COPY . .

# Restore and publish C# app
RUN dotnet restore FinanceMaker.Worker/FinanceMaker.Worker.csproj && \
    dotnet publish FinanceMaker.Worker/FinanceMaker.Worker.csproj -c Release -o /app/publish

# -------------------------------
# Stage 2: Final Runtime Container
# -------------------------------
FROM debian:bullseye-slim

# Install required packages
RUN apt-get update && apt-get install -y \
    unzip wget curl openjdk-11-jre python3 && \
    rm -rf /var/lib/apt/lists/*

# Install .NET Runtime 9.0
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 9.0 && \
    ln -s /root/.dotnet/dotnet /usr/bin/dotnet && \
    rm dotnet-install.sh

# -------------------------------
# Install IB Gateway
# -------------------------------
WORKDIR /opt/ibgateway
RUN wget https://download2.interactivebrokers.com/installers/ibgateway/stable-standalone/ibgateway-stable-standalone-linux-x64.sh && \
    chmod +x ibgateway-stable-standalone-linux-x64.sh && \
    ./ibgateway-stable-standalone-linux-x64.sh --mode unattended

# -------------------------------
# Install IBC (Interactive Brokers Controller)
# -------------------------------
WORKDIR /opt/ibc
RUN wget https://github.com/IbcAlpha/IBC/releases/download/3.23.0/IBCLinux-3.23.0.zip && \
    unzip IBCLinux-3.23.0.zip && rm IBCLinux-3.23.0.zip

# -------------------------------
# Copy published C# app
# -------------------------------
WORKDIR /app
COPY --from=build /app/publish .

# -------------------------------
# Copy startup script and make executable
# -------------------------------
COPY run.sh /run.sh
RUN chmod +x /run.sh

# -------------------------------
# Expose health port for Cloud Run
# -------------------------------
EXPOSE 8080

# -------------------------------
# Entrypoint
# -------------------------------
CMD ["/run.sh"]