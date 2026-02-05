# temporarilly disable the yarn repo
sudo mv /etc/apt/sources.list.d/yarn.list /etc/apt/sources.list.d/yarn.list.bak 2>/dev/null || true

# update azure cli
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash # falls over if above command isnt issued first


