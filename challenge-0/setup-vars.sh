# update azure cli
# curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# login az cli
# az login --use-device-code

export RG_SUFFIX="cm"
export RESOURCE_GROUP="rg-tire-factory-hack-${RG_SUFFIX}"
echo "RESOURCE_GROUP=$RESOURCE_GROUP"

export LOCATION="swedencentral"
echo "LOCATION=$LOCATION"

# get keys for az resources
# ./challenge-0/get-keys.sh --resource-group $RESOURCE_GROUP

export $(cat ./.env | xargs)
echo "AZURE_OPENAI_SERVICE_NAME=$AZURE_OPENAI_SERVICE_NAME"

ME_OBJECT_ID="$(az ad signed-in-user show --query id -o tsv)"
echo "ME_OBJECT_ID=$ME_OBJECT_ID"

OPENAI_RESOURCE_ID="$(az cognitiveservices account show --name "$AZURE_OPENAI_SERVICE_NAME" --resource-group "$RESOURCE_GROUP" --query id -o tsv)"
echo "OPENAI_RESOURCE_ID=$OPENAI_RESOURCE_ID"
