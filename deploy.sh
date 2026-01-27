#!/bin/bash
set -e

# Use the updated Azure CLI from the venv
AZ_CMD="/workspaces/agentic-factory-hack/.venv/bin/python -m azure.cli"

cd /workspaces/agentic-factory-hack/challenge-0

# Set up variables
# Sets the resource group suffix environment variable with a default value of "cm"
# If RG_SUFFIX is already defined, it uses the existing value; otherwise, it defaults to "hack"
export RG_SUFFIX="${RG_SUFFIX:-cm}"
export RESOURCE_GROUP="rg-tire-factory-hack-${RG_SUFFIX}"
# Sets the location environment variable with a default value of "swedencentral"
# If LOCATION is already defined, it uses the existing value; otherwise, it defaults to "swedencentral"
export LOCATION="${LOCATION:-swedencentral}"

echo "=== Deployment Configuration ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Azure CLI Version: $($AZ_CMD --version | head -1)"
echo ""

# Check if we're logged in
echo "Checking Azure login..."
$AZ_CMD account show > /dev/null || {
  echo "Not logged in. Running login..."
  $AZ_CMD login --use-device-code
}

# Create resource group
echo "Creating resource group: $RESOURCE_GROUP..."
$AZ_CMD group create --name $RESOURCE_GROUP --location $LOCATION

# Deploy infrastructure
echo "Deploying infrastructure..."
$AZ_CMD deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/azuredeploy.json \
  --parameters location=$LOCATION

echo ""
echo "=== Deployment Complete ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
