#!/bin/bash

# ----------------------------
# Launch IB Gateway via IBC
# ----------------------------
echo "Starting IB Gateway via IBC..."
cd /opt/ibc/IBC
./IBC.sh &

# ----------------------------
# Give it time to boot
# ----------------------------
echo "Waiting 20 seconds for IB Gateway to boot..."
sleep 20

# ----------------------------
# Start your .NET trading bot
# ----------------------------
echo "Starting FinanceMaker.Worker..."
dotnet /app/FinanceMaker.Worker.dll &

# ----------------------------
# Keep the container alive for Cloud Run
# ----------------------------
echo "Starting Python health server on port 8080..."
python3 -m http.server 8080